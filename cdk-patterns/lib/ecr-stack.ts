import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import * as ecr from 'aws-cdk-lib/aws-ecr';

export interface EcrStackProps extends cdk.StackProps {
  /**
   * List of service repository names under the 'hello-world' namespace
   */
  services?: string[];
  /**
   * Maximum number of images to retain per repository
   * @default 10
   */
  maxImageCount?: number;
}

export class EcrStack extends cdk.Stack {
  public readonly repositories: Record<string, ecr.Repository> = {};

  constructor(scope: Construct, id: string, props?: EcrStackProps) {
    super(scope, id, props);

    const services = props?.services ?? [
      'image-classification',
      'log-analyzer',
      'config-manager',
      'reverse-image-search-backend',
    ];

    const maxImageCount = props?.maxImageCount ?? 10;

    for (const service of services) {
      const repoName = `hello-world/${service}`;
      // PascalCase ID for CloudFormation construct ID (e.g. ImageClassificationRepo)
      const constructId = service
        .split('-')
        .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
        .join('') + 'Repo';

      const repo = new ecr.Repository(this, constructId, {
        repositoryName: repoName,
        imageScanOnPush: true,
        imageTagMutability: ecr.TagMutability.MUTABLE,
        encryption: ecr.RepositoryEncryption.AES_256,
        removalPolicy: cdk.RemovalPolicy.RETAIN,
        lifecycleRules: [
          {
            description: 'Expire untagged images older than 7 days',
            tagStatus: ecr.TagStatus.UNTAGGED,
            maxImageAge: cdk.Duration.days(7),
          },
          {
            description: `Retain a maximum of ${maxImageCount} tagged images`,
            tagStatus: ecr.TagStatus.ANY,
            maxImageCount: maxImageCount,
          },
        ],
      });

      this.repositories[service] = repo;

      new cdk.CfnOutput(this, `${constructId}Uri`, {
        value: repo.repositoryUri,
        description: `ECR Repository URI for ${repoName}`,
        exportName: `HelloWorld-${service}-ECR-Uri`,
      });
    }
  }
}
