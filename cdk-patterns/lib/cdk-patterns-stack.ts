import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import * as s3 from 'aws-cdk-lib/aws-s3';

export class CdkPatternsStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

    // Private S3 bucket for config/dev (S3 names cannot contain '/', using 'config-dev-ACCOUNT')
    const configDevBucket = new s3.Bucket(this, 'ConfigDevBucket', {
      bucketName: `config-dev-${this.account}`,
      blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
      encryption: s3.BucketEncryption.S3_MANAGED,
      enforceSSL: true,
      versioned: true,
      removalPolicy: cdk.RemovalPolicy.DESTROY,
      autoDeleteObjects: true,
    });

    new cdk.CfnOutput(this, 'ConfigDevBucketName', {
      value: configDevBucket.bucketName,
      description: 'The name of the private config/dev S3 bucket',
    });
  }
}
