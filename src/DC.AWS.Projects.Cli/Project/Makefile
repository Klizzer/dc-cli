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

NUGET_FEED_URL ?= [[NUGET_FEED_URL]]
NUGET_API_KEY ?=
CONFIGURE ?= true

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
	@make configure
endif

.PHONY: configure
configure:
	$(DC_CLI_COMMAND) project configure


.PHONY: restore
restore:
	$(DC_CLI_COMMAND) project restore

.PHONY: build
build:
	$(DC_CLI_COMMAND) project build

.PHONY: test
test: build
	$(DC_CLI_COMMAND) project test

.PHONY: start
start: build
	$(DC_CLI_COMMAND) project start

.PHONY: stop
stop:
	$(DC_CLI_COMMAND) project stop

.PHONY: package
package: build
ifeq ($(DETECTED_OS), Windows)
	if exist .packages del .packages
	mkdir .packages
else
	rm -rf ./.packages
	mkdir ./.packages
endif
	$(DC_CLI_COMMAND) infra package -o ./.packages
ifdef NUGET_FEED_URL
ifdef NUGET_API_KEY
	$(foreach file, $(wildcard ./.packages/*), dotnet nuget push $(file) --skip-duplicate  -k $(NUGET_API_KEY) -s $(NUGET_FEED_URL);)
endif
endif
