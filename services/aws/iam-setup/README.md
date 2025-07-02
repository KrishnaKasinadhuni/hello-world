# AWS IAM Setup for System Administrators

This directory contains scripts and instructions for setting up secure IAM access for system administrators.

## Setup Instructions

### 1. Create System Admins Group

```bash
# Create the system-admins group
aws iam create-group --group-name system-admins

# Attach AdministratorAccess policy to the group
aws iam attach-group-policy \
    --group-name system-admins \
    --policy-arn arn:aws:iam::aws:policy/AdministratorAccess
```

### 2. Create Application Admin User

```bash
# Create the application-admin user
aws iam create-user --user-name application-admin

# Add user to system-admins group
aws iam add-user-to-group \
    --user-name application-admin \
    --group-name system-admins

# Create access keys for the user
aws iam create-access-key --user-name application-admin
```

### 3. Create Role for Temporary Access

```bash
# Create the role
aws iam create-role \
    --role-name admin-access-role \
    --assume-role-policy-document '{
        "Version": "2012-10-17",
        "Statement": [
            {
                "Effect": "Allow",
                "Principal": {
                    "AWS": "arn:aws:iam::${AWS_ACCOUNT_NUMBER}:user/application-admin"
                },
                "Action": "sts:AssumeRole"
            }
        ]
    }'

# Attach AdministratorAccess policy to the role
aws iam attach-role-policy \
    --role-name admin-access-role \
    --policy-arn arn:aws:iam::aws:policy/AdministratorAccess
```

### 4. Configure AWS CLI

1. Install AWS CLI if not already installed:
   ```bash
   curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
   unzip awscliv2.zip
   sudo ./aws/install
   ```

2. Configure AWS CLI with application-admin credentials:
   ```bash
   aws configure
   ```
   Enter the following when prompted:
   - AWS Access Key ID: [from step 2]
   - AWS Secret Access Key: [from step 2]
   - Default region name: [your region]
   - Default output format: json

## Usage

### 1. Assume Admin Role

Run the assume-role script:
```bash
./scripts/assume-role.sh
```

This will:
1. Check if current credentials are expired
2. If expired, assume the admin-access-role
3. Update AWS credentials file with temporary credentials
4. Set environment variables for current session

### 2. Verify Access

```bash
# Check current identity
aws sts get-caller-identity

# List S3 buckets (example command)
aws s3 ls
```

## Security Notes

1. **Access Keys**
   - Store access keys securely
   - Rotate keys regularly
   - Never commit keys to version control

2. **Role Assumption**
   - Temporary credentials expire after 1 hour
   - Run assume-role script when credentials expire
   - Monitor role assumption in CloudTrail

3. **Best Practices**
   - Use MFA for additional security
   - Follow principle of least privilege
   - Regularly audit IAM permissions
   - Monitor AWS CloudTrail for suspicious activity

## Troubleshooting

1. **Credential Expiration**
   ```bash
   # Check credential expiration
   aws sts get-caller-identity
   ```

2. **Role Assumption Issues**
   ```bash
   # Check role trust relationship
   aws iam get-role --role-name admin-access-role
   ```

3. **Permission Issues**
   ```bash
   # Check user permissions
   aws iam list-attached-user-policies --user-name application-admin
   aws iam list-user-policies --user-name application-admin
   ``` 