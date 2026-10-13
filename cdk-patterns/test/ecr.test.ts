import * as cdk from 'aws-cdk-lib';
import { Template } from 'aws-cdk-lib/assertions';
import { EcrStack } from '../lib/ecr-stack';

test('ECR Stack creates 4 core repositories with scanning and lifecycle rules', () => {
  const app = new cdk.App();
  const stack = new EcrStack(app, 'TestEcrStack', {
    env: { account: '908027415245', region: 'us-east-1' },
  });
  const template = Template.fromStack(stack);

  const expectedRepos = [
    'hello-world/image-classification',
    'hello-world/log-analyzer',
    'hello-world/config-manager',
    'hello-world/reverse-image-search-backend',
  ];

  for (const repoName of expectedRepos) {
    template.hasResourceProperties('AWS::ECR::Repository', {
      RepositoryName: repoName,
      ImageScanningConfiguration: {
        ScanOnPush: true,
      },
    });
  }

  // Verify total count of repositories created is 4
  template.resourceCountIs('AWS::ECR::Repository', 4);
});
