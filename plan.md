# Serverless dotnet - E01: Intro

What are we going to be building?

* A Phone verification service like the Twilo Verify API.
* Allows users to verify that they actually own the phone number they provide.
* Apps like Uber use phone verification to login.
* Lot's of applications are using phone numbers to provide second factor authentication (2FA). 
* It's easy to make up a fake email address when signing up for an application, but it's a bit harder to do this with a phone number.

Stuff to cover in the intro:

* Walkthrough demo of the application
* Application architecture
* Api design
* AWS Cloud Development Kit (CDK) getting started

# Serverless dotnet - E02: Hello, Lambda

* Setup CDK for this project
* Build a basic "hello world" lambda in dotnet
* AWS Tooling
* Api Gateway

# Serverless dotnet - E03: Sending an SMS

* Request validation
* libphonenumber C#
* Development environment
* Logging with Serilog
* Request timeout
* Setup SNS client

# Serverless dotnet - E04: DynamoDB Setup

* Tidyup
* Setup DynamoDB table using CDK
* Setup DynamoDB client
* Read/Write data
* Explain how we will be using DynamoDB in this application

Recommended videos and articles:

* [DynamoDB Best Practices - Using Sort Keys for Version Control](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/best-practices.html)
* [AWS re:Invent 2018: Amazon DynamoDB Deep Dive: Advanced Design Patterns for DynamoDB (DAT401)](https://www.youtube.com/watch?v=HaEPXoXVf2k)
* [AWS re:Invent 2018: Amazon DynamoDB Under the Hood: How We Built a Hyper-Scale Database (DAT321)](https://www.youtube.com/watch?v=yvBR71D0nAQ&t=2774s)