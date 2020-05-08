SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

DC_CLI_VERSION = [[DC_CLI_VERSION]]
PROJECT_NAME = [[PROJECT_NAME]]

ifeq ($(OS), Windows_NT)
    DETECTED_OS := Windows
    DC_CLI_COMMAND := .\.tools\dc.exe
else
    DETECTED_OS := $(shell sh -c 'uname 2>/dev/null || echo Unknown')
    DC_CLI_COMMAND := ./.tools/dc
endif

NUGET_FEED_URL ?= [[NUGET_FEED_URL]]
NUGET_API_KEY ?=
CONFIGURE ?= true

.DEFAULT_GOAL := build

.PHONY: init
init:
ifeq ($(DETECTED_OS), Windows)
	if not exist .tools mkdir .tools
	if exist .tools/dc.exe del .tools/dc.exe
	(powershell -ExecutionPolicy Bypass -Command "& { iwr https://github.com/DigitalCreationAb/dc-cli/releases/download/v$(DC_CLI_VERSION)/dc-v$(DC_CLI_VERSION)-win-x64.zip -OutFile %cd%\dc.zip }" && 7z.exe x dc.zip -o"%cd%\.tools" && del dc.zip)
else
	mkdir -p ./.tools
	rm -f ./.tools/dc
	wget https://github.com/DigitalCreationAb/dc-cli/releases/download/v$(DC_CLI_VERSION)/dc-v$(DC_CLI_VERSION)-linux-x64.zip
	unzip ./dc-v$(DC_CLI_VERSION)-linux-x64.zip -d ./.tools
	rm ./dc-v$(DC_CLI_VERSION)-linux-x64.zip
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
