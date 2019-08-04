#!/usr/bin/env node
import 'source-map-support/register';
import cdk = require('@aws-cdk/core');
import { AwsCdkPhoneVerifyApiStack } from '../lib/aws-cdk-phone-verify-api-stack';

const app = new cdk.App();

new AwsCdkPhoneVerifyApiStack(app, 'AwsCdkPhoneVerifyApiStack');
