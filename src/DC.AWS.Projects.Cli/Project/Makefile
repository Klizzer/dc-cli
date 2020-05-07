SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

DC_CLI_VERSION = [[DC_CLI_VERSION]]
PROJECT_NAME = [[PROJECT_NAME]]

ifeq ($(OS), Windows_NT)
    DETECTED_OS := Windows
    DC_CLI_COMMAND := .\.tools\dc-aws.exe
else
    DETECTED_OS := $(shell sh -c 'uname 2>/dev/null || echo Unknown')
    DC_CLI_COMMAND := ./.tools/dc-aws
endif

CONFIGURE ?= true
AWS_REGION ?= [[AWS_REGION]]
DEPLOYMENT_BUCKET_NAME ?= $(PROJECT_NAME)-deployments
DEPLOYMENT_STACK_NAME ?= $(DEPLOYMENT_BUCKET_NAME)
VERSION_NUMBER ?= $(shell date +%Y.%m.%d.%H%M%S)
NUGET_FEED_URL ?= [[NUGET_FEED_URL]]
NUGET_API_KEY ?=

.DEFAULT_GOAL := build

.PHONY: init
init:
ifeq ($(DETECTED_OS), Windows)
	if not exist .tools mkdir .tools
	if exist .tools/dc-aws.exe del .tools/dc-aws.exe
	(powershell -ExecutionPolicy Bypass -Command "& { iwr https://github.com/DigitalCreationAb/dc-aws-cli/releases/download/v$(DC_CLI_VERSION)/dc-aws-v$(DC_CLI_VERSION)-win-x64.zip -OutFile %cd%\dc-aws.zip }" && 7z.exe x dc-aws.zip -o"%cd%\.tools" && del dc-aws.zip)
else
	mkdir -p ./.tools
	rm -f ./.tools/dc-aws
	wget https://github.com/DigitalCreationAb/dc-aws-cli/releases/download/v$(DC_CLI_VERSION)/dc-aws-v$(DC_CLI_VERSION)-linux-x64.zip
	unzip ./dc-aws-v$(DC_CLI_VERSION)-linux-x64.zip -d ./.tools
	rm ./dc-aws-v$(DC_CLI_VERSION)-linux-x64.zip
endif
ifeq ($(CONFIGURE), true)
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

.PHONY: start
start: build
	$(DC_CLI_COMMAND) start

.PHONY: stop
stop:
	$(DC_CLI_COMMAND) stop

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
