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

# Serverless dotnet - E05: Generating one-time passwords (HOTP)

* Generating 6 digit one-time password
* Handle the case when the current verification is expired or already verified

Recommended articles:

* [HMAC based One-time password algorithm](https://en.wikipedia.org/wiki/HMAC-based_One-time_Password_algorithm)
* [Otp.NET](https://github.com/kspearrin/Otp.NET)

# Serverless dotnet - E06: DynamoDB Global Secondary Indexes

* Start implementing the verify endpoint
* Lookup verification using Id index by using a GSI

Recommended articles:

* [DynamoDB GSI](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/GSI.html)

# Serverless dotnet - E07: Verifying one-time passwords (HOTP)

* Check not already verified
* Check not expired
* Check max attempts
* Validate HOTP code, 
* Increment attempts if invalid 
* Set verification date if valid

# Serverless dotnet - E08: Usage plans and API Keys

* Protecting our API using API keys
* Usage plan rate limiting
* Create get 'status' endpoint

Recommended articles:

* [Usage plans and API keys](https://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-api-usage-plans.html)
* [API Gateway CDK construct](https://docs.aws.amazon.com/cdk/api/latest/docs/aws-apigateway-readme.html#integration-targets)
* [Rate limiting](https://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-request-throttling.html)

# Serverless dotnet - E09: Refactoring

* Move DynamoDB code into a repository
* Separate each Lambda into it's own project

# Serverless dotnet - E10: Unit testing

* Unit testing and mocks using NSubstitute

Recommended articles:

* [nsubstitute](https://nsubstitute.github.io)

# Serverless dotnet - E11: More unit testing

* Adding unit tests to the check endpoint

# Serverless dotnet - E12: Building with Github Actions

* Build the code using Github actions
* Run the unit tests
* Deploy the code to AWS

Recommended articles:

* [Github Actions](https://github.com/features/actions)
* [Github Actions docs](https://help.github.com/en/articles/workflow-syntax-for-github-actions)

# Serverless dotnet - E13: Deploying with Github Actions

* Deploying to AWS using CDK

* [Github Actions: Contexts and expressions](https://help.github.com/en/articles/contexts-and-expression-syntax-for-github-actions)
* [Github Actions: Virtual environments](https://help.github.com/en/articles/virtual-environments-for-github-actions)

# Serverless dotnet - E14: Integration tests with Github Actions

* Add an integration tests project
* Run the integration tests after deploying to AWS