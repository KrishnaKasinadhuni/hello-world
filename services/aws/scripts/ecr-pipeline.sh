#!/bin/bash
# ==============================================================================
# AWS ECR Pipeline & Container Publishing Manager
# Manages ECR repositories, authentication, container builds, and uploads
# for hello-world core microservices.
# ==============================================================================

set -eo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKSPACE_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

# AWS Configuration
AWS_REGION="${AWS_REGION:-us-east-1}"
AWS_PROFILE="${AWS_PROFILE:-assumed-admin}"

# Services Catalog: Format is key|path_relative_to_workspace|repo_name
SERVICES=(
    "image-classification|services/nodejs/imageClassification|hello-world/image-classification"
    "log-analyzer|services/python/logAnalyzer|hello-world/log-analyzer"
    "config-manager|services/python/configManager|hello-world/config-manager"
    "reverse-image-search-backend|reverse-image-search-aws/backend|hello-world/reverse-image-search-backend"
)

# Colors for terminal output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

log_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# ------------------------------------------------------------------------------
# Ensure AWS credentials are valid
# ------------------------------------------------------------------------------
check_aws_auth() {
    log_info "Validating AWS credentials for profile '$AWS_PROFILE'..."
    if ! aws sts get-caller-identity --profile "$AWS_PROFILE" > /dev/null 2>&1; then
        log_warn "Credentials for '$AWS_PROFILE' are expired or missing. Attempting renewal..."
        local assume_script="$WORKSPACE_ROOT/services/aws/iam-setup/scripts/assume-role.sh"
        if [ -f "$assume_script" ]; then
            AWS_PROFILE=app "$assume_script" || {
                log_error "Failed to assume role. Check base AWS credentials."
                exit 1
            }
        else
            log_error "assume-role.sh script not found at $assume_script"
            exit 1
        fi
    fi

    ACCOUNT_ID=$(aws sts get-caller-identity --profile "$AWS_PROFILE" --query Account --output text)
    ECR_REGISTRY="${ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"
    log_success "AWS authenticated as Account: $ACCOUNT_ID (Registry: $ECR_REGISTRY)"
}

# ------------------------------------------------------------------------------
# Check Docker Daemon
# ------------------------------------------------------------------------------
check_docker_daemon() {
    if ! docker info > /dev/null 2>&1; then
        log_error "Docker daemon is not running."
        log_info "On macOS, launch Docker Desktop using: 'open -a Docker'"
        exit 1
    fi
}

# ------------------------------------------------------------------------------
# Authenticate Docker to AWS ECR
# ------------------------------------------------------------------------------
cmd_login() {
    check_aws_auth
    check_docker_daemon
    log_info "Logging in Docker to ECR registry $ECR_REGISTRY..."
    aws ecr get-login-password --region "$AWS_REGION" --profile "$AWS_PROFILE" | \
        docker login --username AWS --password-stdin "$ECR_REGISTRY"
    log_success "Successfully authenticated Docker with ECR registry."
}

# ------------------------------------------------------------------------------
# Setup / Create ECR Repositories idempotently
# ------------------------------------------------------------------------------
cmd_setup() {
    check_aws_auth

    # Lifecycle policy JSON: expire untagged after 7 days, keep max 10 images
    local lifecycle_policy='{
        "rules": [
            {
                "rulePriority": 1,
                "description": "Expire untagged images older than 7 days",
                "selection": {
                    "tagStatus": "untagged",
                    "countType": "sinceImagePushed",
                    "countUnit": "days",
                    "countNumber": 7
                },
                "action": { "type": "expire" }
            },
            {
                "rulePriority": 2,
                "description": "Retain maximum 10 images",
                "selection": {
                    "tagStatus": "any",
                    "countType": "imageCountMoreThan",
                    "countNumber": 10
                },
                "action": { "type": "expire" }
            }
        ]
    }'

    for item in "${SERVICES[@]}"; do
        IFS="|" read -r key rel_path repo_name <<< "$item"
        log_info "Checking ECR repository: $repo_name"

        if aws ecr describe-repositories --repository-names "$repo_name" --profile "$AWS_PROFILE" --region "$AWS_REGION" > /dev/null 2>&1; then
            log_info "Repository '$repo_name' already exists."
        else
            log_info "Creating ECR repository: $repo_name..."
            aws ecr create-repository \
                --repository-name "$repo_name" \
                --image-scanning-configuration scanOnPush=true \
                --encryption-configuration encryptionType=AES256 \
                --image-tag-mutability MUTABLE \
                --profile "$AWS_PROFILE" \
                --region "$AWS_REGION" > /dev/null
            log_success "Created repository '$repo_name'."
        fi

        log_info "Applying lifecycle policy to '$repo_name'..."
        aws ecr put-lifecycle-policy \
            --repository-name "$repo_name" \
            --lifecycle-policy-text "$lifecycle_policy" \
            --profile "$AWS_PROFILE" \
            --region "$AWS_REGION" > /dev/null
    done

    log_success "All ECR repositories provisioned and configured with lifecycle policies."
}

# ------------------------------------------------------------------------------
# Build Docker Images
# ------------------------------------------------------------------------------
cmd_build() {
    local target_key="${1:-all}"
    check_aws_auth
    check_docker_daemon

    local git_sha
    git_sha="$(git -C "$WORKSPACE_ROOT" rev-parse --short HEAD 2>/dev/null || echo "local")"

    local found=false
    for item in "${SERVICES[@]}"; do
        IFS="|" read -r key rel_path repo_name <<< "$item"
        if [ "$target_key" != "all" ] && [ "$target_key" != "$key" ]; then
            continue
        fi
        found=true

        local context_dir="$WORKSPACE_ROOT/$rel_path"
        log_info "Building image for '$key' from $context_dir..."

        local full_repo="$ECR_REGISTRY/$repo_name"
        docker build \
            --platform "linux/amd64" \
            -t "$repo_name:latest" \
            -t "$repo_name:$git_sha" \
            -t "$full_repo:latest" \
            -t "$full_repo:$git_sha" \
            "$context_dir"

        log_success "Successfully built '$key' (tags: latest, $git_sha)"
    done

    if [ "$found" = false ]; then
        log_error "Unknown service key '$target_key'. Available keys: image-classification, log-analyzer, config-manager, reverse-image-search-backend, all"
        exit 1
    fi
}

# ------------------------------------------------------------------------------
# Push Docker Images to ECR
# ------------------------------------------------------------------------------
cmd_push() {
    local target_key="${1:-all}"
    check_aws_auth
    check_docker_daemon
    cmd_login

    local git_sha
    git_sha="$(git -C "$WORKSPACE_ROOT" rev-parse --short HEAD 2>/dev/null || echo "local")"

    local found=false
    for item in "${SERVICES[@]}"; do
        IFS="|" read -r key rel_path repo_name <<< "$item"
        if [ "$target_key" != "all" ] && [ "$target_key" != "$key" ]; then
            continue
        fi
        found=true

        local full_repo="$ECR_REGISTRY/$repo_name"
        log_info "Pushing '$key' images to $full_repo..."

        docker push "$full_repo:latest"
        docker push "$full_repo:$git_sha"

        log_success "Pushed '$key' images to ECR (tags: latest, $git_sha)"
    done

    if [ "$found" = false ]; then
        log_error "Unknown service key '$target_key'."
        exit 1
    fi
}

# ------------------------------------------------------------------------------
# Build and Push in a single step
# ------------------------------------------------------------------------------
cmd_build_push() {
    local target_key="${1:-all}"
    cmd_build "$target_key"
    cmd_push "$target_key"
}

# ------------------------------------------------------------------------------
# Status: Inspect ECR Repositories and Images
# ------------------------------------------------------------------------------
cmd_status() {
    check_aws_auth

    echo ""
    echo -e "${CYAN}=== AWS ECR Repositories for hello-world ===${NC}"
    printf "%-32s %-50s %-12s\n" "SERVICE" "REPOSITORY URI" "IMAGE COUNT"
    echo "----------------------------------------------------------------------------------------------------"

    for item in "${SERVICES[@]}"; do
        IFS="|" read -r key rel_path repo_name <<< "$item"
        local uri="$ECR_REGISTRY/$repo_name"
        local count="0"
        if aws ecr describe-repositories --repository-names "$repo_name" --profile "$AWS_PROFILE" --region "$AWS_REGION" > /dev/null 2>&1; then
            count=$(aws ecr list-images --repository-name "$repo_name" --profile "$AWS_PROFILE" --region "$AWS_REGION" --query 'imageIds | length(@)' --output text 2>/dev/null || echo "0")
            printf "%-32s %-50s %-12s\n" "$key" "$uri" "$count"
        else
            printf "%-32s %-50s %-12s\n" "$key" "(not created yet)" "-"
        fi
    done
    echo ""
}

# ------------------------------------------------------------------------------
# Help
# ------------------------------------------------------------------------------
cmd_help() {
    echo "Usage: $(basename "$0") <command> [service_key]"
    echo ""
    echo "Commands:"
    echo "  setup                 Create/verify the 4 ECR repositories & attach lifecycle policies"
    echo "  login                 Authenticate local Docker CLI with AWS ECR"
    echo "  build [key|all]       Build Docker images for services (default: all)"
    echo "  push [key|all]        Push tagged Docker images to ECR (default: all)"
    echo "  build-push [key|all]  Build and push images in one step"
    echo "  status                Show status of all ECR repositories and uploaded images"
    echo ""
    echo "Available service keys:"
    echo "  - image-classification"
    echo "  - log-analyzer"
    echo "  - config-manager"
    echo "  - reverse-image-search-backend"
    echo "  - all (default)"
    echo ""
    echo "Environment Variables:"
    echo "  AWS_PROFILE   AWS profile name (default: assumed-admin)"
    echo "  AWS_REGION    AWS region (default: us-east-1)"
}

# ------------------------------------------------------------------------------
# Entrypoint Dispatcher
# ------------------------------------------------------------------------------
ACTION="${1:-help}"
TARGET="${2:-all}"

case "$ACTION" in
    setup|create-repos) cmd_setup ;;
    login)              cmd_login ;;
    build)              cmd_build "$TARGET" ;;
    push)               cmd_push "$TARGET" ;;
    build-push)         cmd_build_push "$TARGET" ;;
    status)             cmd_status ;;
    help|--help|-h)     cmd_help ;;
    *)
        log_error "Unknown command '$ACTION'"
        cmd_help
        exit 1
        ;;
esac
