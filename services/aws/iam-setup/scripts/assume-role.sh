#!/bin/bash

# Configuration
ROLE_NAME="admin-access-role"
PROFILE_NAME="assumed-admin" # Define the profile name
SESSION_NAME="admin-session-$(date +%Y%m%d-%H%M%S)"
CREDENTIALS_FILE="$HOME/.aws/credentials"
# ENV_FILE="$HOME/.aws/credentials.env" # No longer needed

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored messages
print_message() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

# Function to check if the named profile credentials are valid
check_credentials() {
    print_message "$YELLOW" "Checking credentials for profile '$PROFILE_NAME'..."
    # Try to get caller identity using the profile. Exit code 0 means success (valid).
    if aws sts get-caller-identity --profile "$PROFILE_NAME" > /dev/null 2>&1; then
        return 0 # Credentials are valid
    else
        # Optional: Check specific error (e.g., ExpiredToken), but failure is enough for now
        local exit_code=$?
        print_message "$YELLOW" "Credentials for profile '$PROFILE_NAME' are invalid or expired (aws cli exit code: $exit_code)."
        return 1 # Credentials are expired or invalid
    fi
}

# Function to assume role and get temporary credentials
assume_role() {
    print_message "$YELLOW" "Attempting to assume role '$ROLE_NAME' using base credentials..."

    # Determine the account ID from the base credentials
    local account_id=$(aws sts get-caller-identity --query Account --output text)
    if [ -z "$account_id" ]; then
        print_message "$RED" "Failed to determine AWS Account ID from base credentials. Ensure AWS CLI is configured."
        exit 1
    fi
    local role_arn="arn:aws:iam::$account_id:role/$ROLE_NAME"
    print_message "$YELLOW" "Target Role ARN: $role_arn"

    # Get temporary credentials using base credentials (default profile or AWS_PROFILE env var)
    local credentials=$(aws sts assume-role \
        --role-arn "$role_arn" \
        --role-session-name "$SESSION_NAME" \
        --duration-seconds 3600) # Duration can be adjusted (900-43200)

    if [ $? -ne 0 ]; then
        print_message "$RED" "Failed to assume role '$ROLE_NAME'."
        print_message "$RED" "Check base credentials permissions and the role's trust policy."
        exit 1
    fi

    # Extract credentials
    local access_key=$(echo "$credentials" | jq -r .Credentials.AccessKeyId)
    local secret_key=$(echo "$credentials" | jq -r .Credentials.SecretAccessKey)
    local session_token=$(echo "$credentials" | jq -r .Credentials.SessionToken)
    local expiration=$(echo "$credentials" | jq -r .Credentials.Expiration)

    if [ -z "$access_key" ] || [ -z "$secret_key" ] || [ -z "$session_token" ]; then
        print_message "$RED" "Failed to parse temporary credentials. Is jq installed?"
        exit 1
    fi

    # Update credentials file using aws configure set for atomicity
    print_message "$YELLOW" "Updating profile '$PROFILE_NAME' in $CREDENTIALS_FILE..."
    aws configure set aws_access_key_id "$access_key" --profile "$PROFILE_NAME"
    aws configure set aws_secret_access_key "$secret_key" --profile "$PROFILE_NAME"
    aws configure set aws_session_token "$session_token" --profile "$PROFILE_NAME"
    # Optionally set region if needed for the profile
    # local region=$(aws configure get region)
    # if [ -n "$region" ]; then
    #   aws configure set region "$region" --profile "$PROFILE_NAME"
    # fi

    # Remove the old ENV_FILE if it exists
    # if [ -f "$ENV_FILE" ]; then rm "$ENV_FILE"; fi # Cleanup

    print_message "$GREEN" "Successfully updated profile '$PROFILE_NAME'."
    print_message "$YELLOW" "Credentials will expire at: $expiration"
    print_message "$YELLOW" "To use these credentials, add '--profile $PROFILE_NAME' to AWS CLI commands or set AWS_PROFILE=$PROFILE_NAME"
}

# Main script
# Check credentials first
if check_credentials; then
    print_message "$GREEN" "Credentials for profile '$PROFILE_NAME' are currently valid."
    print_message "$YELLOW" "Current identity for profile '$PROFILE_NAME':"
    aws sts get-caller-identity --profile "$PROFILE_NAME"
    # Optionally add a prompt here to force refresh if needed
    # read -p "Refresh credentials anyway? (y/N): " refresh_choice
    # if [[ "$refresh_choice" =~ ^[Yy]$ ]]; then
    #     assume_role
    # fi
else
    assume_role
fi

# Verify the credentials using the profile
print_message "$YELLOW" "Verifying credentials for profile '$PROFILE_NAME' one last time..."
if aws sts get-caller-identity --profile "$PROFILE_NAME" > /dev/null 2>&1; then
    print_message "$GREEN" "Successfully verified credentials for profile '$PROFILE_NAME'."
else
    # This shouldn't happen if assume_role succeeded, but good to check
    print_message "$RED" "Failed to verify credentials for profile '$PROFILE_NAME' immediately after update."
    exit 1
fi 