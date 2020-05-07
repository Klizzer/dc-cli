SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

JJ_VERSION = 1.2.3
YQ_VERSION = 3.3.0
TERRAFORM_VERSION ?= 0.12.24
DC_CLI_VERSION = [[DC_CLI_VERSION]]
LOCALSTACK_VERSION = latest
LOCALSTACK_SERVICES = 'edge,apigateway,cloudformation,dynamodb,iam,lambda,logs,s3,sts'
PROJECT_NAME = [[PROJECT_NAME]]

ifeq ($(OS), Windows_NT)
    DETECTED_OS := Windows
    DC_CLI_COMMAND := .\.tools\dc-aws.exe
    TERRAFORM_CLI_COMMAND := .\.tools\terraform.exe
else
    DETECTED_OS := $(shell sh -c 'uname 2>/dev/null || echo Unknown')
    DC_CLI_COMMAND := ./.tools/dc-aws
    TERRAFORM_CLI_COMMAND := ./.tools/terraform
endif

AWS_REGION ?= [[AWS_REGION]]
DEPLOYMENT_BUCKET_NAME ?= $(PROJECT_NAME)-deployments
DEPLOYMENT_STACK_NAME ?= $(DEPLOYMENT_BUCKET_NAME)
VERSION_NUMBER ?= $(shell date +%Y.%m.%d.%H%M%S)
NUGET_FEED_URL ?= [[NUGET_FEED_URL]]
NUGET_API_KEY ?=
CONFIGURE ?= true

.DEFAULT_GOAL := build

.PHONY: init
init:
ifeq ($(DETECTED_OS), Windows)
	if not exist .tools mkdir .tools
	if exist .tools/jj.exe del .tools/jj.exe
	if exist .tools/dc-aws.exe del .tools/dc-aws.exe
	(powershell -ExecutionPolicy Bypass -Command "& { iwr https://github.com/tidwall/jj/releases/download/v$(JJ_VERSION)/jj-$(JJ_VERSION)-windows-amd64.zip -OutFile %cd%\jj.zip }" && 7z.exe x jj.zip -o"%cd%\.tools" && del jj.zip)
	(powershell -ExecutionPolicy Bypass -Command "& { iwr https://github.com/DigitalCreationAb/dc-aws-cli/releases/download/v$(DC_CLI_VERSION)/dc-aws-v$(DC_CLI_VERSION)-win-x64.zip -OutFile %cd%\dc-aws.zip }" && 7z.exe x dc-aws.zip -o"%cd%\.tools" && del dc-aws.zip)
else
	mkdir -p ./.tools
	
	wget https://releases.hashicorp.com/terraform/$(TERRAFORM_VERSION)/terraform_$(TERRAFORM_VERSION)_linux_amd64.zip
	rm -f ./.tools/terraform
	unzip ./terraform_$(TERRAFORM_VERSION)_linux_amd64.zip -d ./.tools
	rm ./terraform_$(TERRAFORM_VERSION)_linux_amd64.zip
	wget https://github.com/tidwall/jj/releases/download/v$(JJ_VERSION)/jj-$(JJ_VERSION)-linux-amd64.tar.gz
	tar -xvzf ./jj-$(JJ_VERSION)-linux-amd64.tar.gz -C ./.tools
	
	rm -f ./.tools/jj
	mv ./.tools/jj-$(JJ_VERSION)-linux-amd64/jj ./.tools
	rm -rf ./.tools/jj-$(JJ_VERSION)-linux-amd64
	rm ./jj-$(JJ_VERSION)-linux-amd64.tar.gz
	
	rm -f ./.tools/dc-aws
	wget https://github.com/DigitalCreationAb/dc-aws-cli/releases/download/v$(DC_CLI_VERSION)/dc-aws-v$(DC_CLI_VERSION)-linux-x64.zip
	unzip ./dc-aws-v$(DC_CLI_VERSION)-linux-x64.zip -d ./.tools
	rm ./dc-aws-v$(DC_CLI_VERSION)-linux-x64.zip
	
	rm -f ./.tools/yq
	wget -O ./.tools/yq https://github.com/mikefarah/yq/releases/download/$(YQ_VERSION)/yq_linux_amd64
	chmod +x ./.tools/yq
endif
	$(TERRAFORM_CLI_COMMAND) init -backend=false ./infrastructure/environment
ifeq ($(CONFIGURE), true)
	$(foreach directory, $(wildcard ./infrastructure/containers/*), docker build -t "$(PROJECT_NAME)/$(notdir $(directory))" $(directory);)
	$(DC_CLI_COMMAND) configure -s
endif

.PHONY: restore
restore:
	$(DC_CLI_COMMAND) restore

.PHONY: build
build:
	$(DC_CLI_COMMAND) build

.PHONY: test
test: build
	$(DC_CLI_COMMAND) test
	$(TERRAFORM_CLI_COMMAND) validate ./infrastructure/environment
	aws cloudformation validate-template --template-body file://./infrastructure/environment/.generated/project.yml

.PHONY: start
start: build
ifdef service
	@make -f ./services/$(service).make PROJECT_NAME=$(PROJECT_NAME) start
else
	$(foreach file, $(wildcard ./services/*.make), make -f $(file) PROJECT_NAME=$(PROJECT_NAME) start &)
	wait
	echo "Everything up and running."
endif

.PHONY: stop
stop:
ifdef service
	@make -f ./services/$(service).make PROJECT_NAME=$(PROJECT_NAME) stop
else
	$(foreach file, $(wildcard ./services/*.make), make -f $(file) PROJECT_NAME=$(PROJECT_NAME) stop &)
	wait
	echo "Everything stopped."
endif

.PHONY: logs
logs:
ifdef service
	@make -f ./services/$(service).make PROJECT_NAME=$(PROJECT_NAME) logs
else
	$(foreach file, $(wildcard ./services/*.make), make -f $(file) PROJECT_NAME=$(PROJECT_NAME) logs;)
endif

.PHONY: package
package: build
ifeq ($(DETECTED_OS), Windows)
	if exist .packages del .packages
	mkdir .packages
	copy /B /Y .\infrastructure\environment\.generated\project.yml .\project.$(VERSION_NUMBER).yml
else
	rm -rf ./.packages
	mkdir ./.packages
	cp ./infrastructure/environment/.generated/project.yml ./project.$(VERSION_NUMBER).yml
endif
	aws cloudformation deploy \
		--template-file ./infrastructure/deployment-bucket.yml \
		--stack-name $(DEPLOYMENT_STACK_NAME) \
		--parameter-overrides DeploymentBucketName=$(DEPLOYMENT_BUCKET_NAME) \
		--no-fail-on-empty-changeset \
		--region $(AWS_REGION)
	aws cloudformation package \
		--template-file ./project.$(VERSION_NUMBER).yml \
		--output-template-file ./infrastructure/environment/.generated/project.packaged.yml \
		--s3-bucket $(DEPLOYMENT_BUCKET_NAME) \
		--s3-prefix $(VERSION_NUMBER)
ifeq ($(DETECTED_OS), Windows)
	del .\project.$(VERSION_NUMBER).yml
else
	zip -r ./.packages/$(PROJECT_NAME).$(VERSION_NUMBER).zip ./infrastructure/environment/
	rm ./project.$(VERSION_NUMBER).yml
endif
ifdef NUGET_FEED_URL
	$(foreach file, $(wildcard ./.packages/*), dotnet nuget push $(file) --skip-duplicate  -k $(NUGET_API_KEY) -s $(NUGET_FEED_URL);)
endif

.PHONY: clear
clear: stop
ifeq ($(DETECTED_OS), Windows)
	if exist .localstack del .localstack
	if exist .packages del .packages
else
	sudo rm -rf ./.localstack
	rm -rf ./.packages
endif
