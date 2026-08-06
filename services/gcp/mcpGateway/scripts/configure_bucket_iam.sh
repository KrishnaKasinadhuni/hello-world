#!/usr/bin/env bash
# Configure Secure IAM Access Rules and Public Access Prevention for GCS Memory Bucket

set -euo pipefail

BUCKET_NAME="${1:-mcp-memory-precise-works-456015-h9}"
PROJECT_ID="${2:-precise-works-456015-h9}"
SERVICE_ACCOUNT="839475551602-compute@developer.gserviceaccount.com"
ADMIN_USER="krishna.kasinadhuni@gmail.com"

echo "🔒 Configuring IAM Security Rules for Bucket: gs://${BUCKET_NAME}..."

# 1. Enable Uniform Bucket-Level Access
echo "1️⃣ Enabling Uniform Bucket-Level Access..."
gcloud storage buckets update "gs://${BUCKET_NAME}" \
  --project="${PROJECT_ID}" \
  --uniform-bucket-level-access

# 2. Enforce Public Access Prevention
echo "2️⃣ Enforcing Public Access Prevention (Block All Public Access)..."
gcloud storage buckets update "gs://${BUCKET_NAME}" \
  --project="${PROJECT_ID}" \
  --public-access-prevention

# 3. Grant ObjectUser to Cloud Run Service Account
echo "3️⃣ Granting roles/storage.objectUser to Cloud Run Service Account (${SERVICE_ACCOUNT})..."
gcloud storage buckets add-iam-policy-binding "gs://${BUCKET_NAME}" \
  --project="${PROJECT_ID}" \
  --member="serviceAccount:${SERVICE_ACCOUNT}" \
  --role="roles/storage.objectUser"

# 4. Grant ObjectAdmin to Developer Admin
echo "4️⃣ Granting roles/storage.objectAdmin to Developer (${ADMIN_USER})..."
gcloud storage buckets add-iam-policy-binding "gs://${BUCKET_NAME}" \
  --project="${PROJECT_ID}" \
  --member="user:${ADMIN_USER}" \
  --role="roles/storage.objectAdmin"

# 5. Apply 30-Day Object Lifecycle Expiration Policy
echo "5️⃣ Applying 30-Day Object Lifecycle Policy (Delete objects older than 30 days)..."
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LIFECYCLE_FILE="${SCRIPT_DIR}/../lifecycle.json"
gcloud storage buckets update "gs://${BUCKET_NAME}" \
  --project="${PROJECT_ID}" \
  --lifecycle-file="${LIFECYCLE_FILE}"

echo "✅ GCS Bucket IAM Security & 30-Day Lifecycle Configuration Complete!"
