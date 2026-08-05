import * as cdk from 'aws-cdk-lib';
import { Template } from 'aws-cdk-lib/assertions';
import * as CdkPatterns from '../lib/cdk-patterns-stack';

test('Private S3 Bucket Created', () => {
  const app = new cdk.App();
  const stack = new CdkPatterns.CdkPatternsStack(app, 'MyTestStack', {
    env: { account: '908027415245', region: 'us-east-1' }
  });
  const template = Template.fromStack(stack);

  template.hasResourceProperties('AWS::S3::Bucket', {
    BucketName: 'config-dev-908027415245',
    PublicAccessBlockConfiguration: {
      BlockPublicAcls: true,
      BlockPublicPolicy: true,
      IgnorePublicAcls: true,
      RestrictPublicBuckets: true
    }
  });
});
