import {RemovalPolicy, Stack, StackProps} from 'aws-cdk-lib';
import {Table, AttributeType, BillingMode} from 'aws-cdk-lib/aws-dynamodb';
import { AccountPrincipal, Role } from 'aws-cdk-lib/aws-iam';
import { Construct } from 'constructs';
import { CfnGlobalReplicationGroup, CfnReplicationGroup} from 'aws-cdk-lib/aws-elasticache'
import { glob } from 'fs';
// import * as sqs from 'aws-cdk-lib/aws-sqs';

export class DynamoStack extends Stack {
  constructor(scope: Construct, id: string, props?: StackProps) {
    super(scope, id, props);
// create a DynamoDB table
    const table = new Table(this, 'Table-mytable', {
      partitionKey: { name: 'id', type: AttributeType.STRING },
      removalPolicy: RemovalPolicy.DESTROY,
      billingMode: BillingMode.PAY_PER_REQUEST,
      tableName: 'MyTable'
    });

    // create a role
    const role = new Role(this, 'Role', {
      assumedBy: new AccountPrincipal('908027415245')
    });

    // table.grantReadWriteData(role);
    table.grantFullAccess(role);
  }
}

export class PrimaryCacheClusterStack extends Stack {
  constructor(scope: Construct, id: string, props?: StackProps) {
    super(scope, id, props);
   const primaryCluster = new CfnReplicationGroup(this, 'ReplicationGroup', {
      replicationGroupDescription: 'primarycluster',
      cacheNodeType: 'cache.t3.micro',
      engine: 'redis',
      cacheSubnetGroupName: 'default',
      numNodeGroups: 1,
      replicasPerNodeGroup: 1
    });
  }
}
export interface GlobalDataStoreStackProps extends StackProps {
  primaryStack: CfnReplicationGroup;
}

export class GlobalDataStoreStack extends Stack {
  constructor(scope: Construct, id: string, props: GlobalDataStoreStackProps) {
    super(scope, id, props);
   new CfnGlobalReplicationGroup(this, 'GlobalReplicationGroup', {
      globalReplicationGroupIdSuffix: 'myglobalreplicationgroup',
      globalNodeGroupCount: 1,
      cacheNodeType: 'cache.t3.micro',
      engine: 'redis',
      members: [  { replicationGroupRegion: 'us-east-1', role : 'PRIMARY', replicationGroupId: props.primaryStack.ref } ],

    });
  }
}

export interface secondaryClusterStackProps extends StackProps {
  globalStack: CfnGlobalReplicationGroup;
}

export class secondaryClusterStack extends Stack {
  constructor(scope: Construct, id: string, props: secondaryClusterStackProps) {
    super(scope, id, props);
   new CfnReplicationGroup(this, 'ReplicationGroup', {
      globalReplicationGroupId: props.globalStack.ref,
      replicationGroupDescription: 'secondarycluster',
      cacheNodeType: 'cache.t3.micro',
      engine: 'redis',
      cacheSubnetGroupName: 'default',
      numNodeGroups: 1,
      replicasPerNodeGroup: 1
    });
  }
}
