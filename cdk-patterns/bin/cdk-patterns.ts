#!/usr/bin/env node
import * as cdk from 'aws-cdk-lib';
import { CdkPatternsStack } from '../lib/cdk-patterns-stack';
import { EcrStack } from '../lib/ecr-stack';

const app = new cdk.App();
const env = {
  account: process.env.CDK_DEFAULT_ACCOUNT || '908027415245',
  region: process.env.CDK_DEFAULT_REGION || 'us-east-1',
};

new CdkPatternsStack(app, 'CdkPatternsStack', { env });
new EcrStack(app, 'HelloWorldEcrStack', { env });