SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

JJ_VERSION = 1.2.3
NGINX_VERSION = 1.18.0
LOCALSTACK_VERSION = latest
LOCALSTACK_SERVICES = 'edge,apigateway,cloudformation,dynamodb,iam,lambda,logs,s3,sts'
PROJECT_NAME = [[PROJECT_NAME]]

ifeq ($(OS), Windows_NT)
    DETECTED_OS := Windows
else
    DETECTED_OS := $(shell sh -c 'uname 2>/dev/null || echo Unknown')
endif

AWS_REGION ?= us-east-1
VERSION_NUMBER ?= $(shell date +%Y.%m.%d.%H%M%S)

.DEFAULT_GOAL := build

.PHONY: init
init:
ifeq (DETECTED_OS, Windows)
	if not exist .tools mkdir .tools
	if exist .tools/jj.exe del .tools/jj.exe
	(powershell -ExecutionPolicy Bypass -Command "& { iwr https://github.com/tidwall/jj/releases/download/v$(JJ_VERSION)/jj-$(JJ_VERSION)-windows-amd64.zip -OutFile %cd%\jj.zip }" && 7z.exe x jj.zip -o"%cd%\.tools" && del jj.zip)
else
	mkdir -p ./.tools
	wget https://github.com/tidwall/jj/releases/download/v$(JJ_VERSION)/jj-$(JJ_VERSION)-linux-amd64.tar.gz
	tar -xvzf ./jj-$(JJ_VERSION)-linux-amd64.tar.gz -C ./.tools
	rm -f ./.tools/jj
	mv ./.tools/jj-$(JJ_VERSION)-linux-amd64/jj ./.tools
	rm -rf ./.tools/jj-$(JJ_VERSION)-linux-amd64
	rm ./jj-$(JJ_VERSION)-linux-amd64.tar.gz
endif
	$(foreach directory, $(wildcard ./infrastructure/containers/*), docker build -t "$(PROJECT_NAME)/$(notdir $(directory))" $(directory);)
	dc-aws configure -s

.PHONY: build
build:
	dc-aws build

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
	$(foreach file, $(wildcard ./services/*.make), make -f $(file) PROJECT_NAME=$(PROJECT_NAME) logs)
endif

.PHONY: clear
clear: stop
ifeq (DETECTED_OS, Windows)
	if exist .localstack del .localstack
else
	sudo rm -rf ./.localstack
endif
