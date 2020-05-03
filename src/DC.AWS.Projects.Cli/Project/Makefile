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

.PHONY: build
build:
	dc-aws build

.PHONY: init.infra
init.infra:
	$(foreach directory, $(wildcard ./infrastructure/containers/*), docker build -t "$(PROJECT_NAME)/$(notdir $(directory))" $(directory);)

.PHONY: init
init:
	mkdir -p ./.tools
	wget https://github.com/tidwall/jj/releases/download/v$(JJ_VERSION)/jj-$(JJ_VERSION)-linux-amd64.tar.gz
	tar -xvzf ./jj-$(JJ_VERSION)-linux-amd64.tar.gz -C ./.tools
	rm -f ./.tools/jj
	mv ./.tools/jj-$(JJ_VERSION)-linux-amd64/jj ./.tools
	rm -rf ./.tools/jj-$(JJ_VERSION)-linux-amd64
	rm ./jj-$(JJ_VERSION)-linux-amd64.tar.gz
	@make clear init.infra
	dc-aws configure -s

.PHONY: ensure.localstack
ensure.localstack:
	dc-aws ensure-localstack -s $(LOCALSTACK_SERVICES)

.PHONY: ensure.infra
ensure.infra: ensure.localstack
	dc-aws ensure-infra

.PHONY: stop.localstack
stop.localstack:
	docker stop $(PROJECT_NAME)-localstack || true
	docker container rm $(PROJECT_NAME)-localstack || true

.PHONY: start.localstack
start.localstack: stop.localstack
	mkdir -p ./.localstack
	$(eval LOCALSTACK_API_KEY=`./.tools/jj -i ./.settings.json localstackApiKey`)
	docker pull localstack/localstack:$(LOCALSTACK_VERSION)
	docker run --name $(PROJECT_NAME)-localstack -d -p 4563-4599:4563-4599 -p 8055:8080 -v $(CURDIR)/.localstack:/tmp/localstack -v /var/run/docker.sock:/var/run/docker.sock -e SERVICES=$(LOCALSTACK_SERVICES) -e DATA_DIR='/tmp/localstack/data' -e LOCALSTACK_API_KEY='$(LOCALSTACK_API_KEY)' -e LAMBDA_REMOTE_DOCKER=0 -e DEBUG=1 localstack/localstack:$(LOCALSTACK_VERSION)

.PHONY: stop.proxy
stop.proxy:
	$(foreach file, $(wildcard ./config/.generated/*.proxy), docker stop $(PROJECT_NAME)-proxy-$(basename $(notdir $(file))) || true;)
	$(foreach file, $(wildcard ./config/.generated/*.proxy), docker container rm $(PROJECT_NAME)-proxy-$(basename $(notdir $(file))) || true;)

.PHONY: start.proxy
start.proxy: stop.proxy build
	dc-aws setup-proxy
	docker pull nginx
	$(foreach file, $(wildcard ./config/.generated/*.proxy), $(eval PORT = `./.tools/jj -i $(file) port`) $(eval RELATIVE_PATH = `./.tools/jj -i $(file) path`) docker run --name $(PROJECT_NAME)-proxy-$(basename $(notdir $(file))) -d -v "$(CURDIR)/$(RELATIVE_PATH)/proxy.nginx.conf:/etc/nginx/nginx.conf" -v "$(CURDIR)/$(RELATIVE_PATH)/_child_paths:/etc/nginx/_child_paths" -p $(PORT):80 nginx;)

.PHONY: stop.api
stop.api:
	$(foreach file, $(wildcard ./infrastructure/environment/.generated/*.api.yml), docker stop $(PROJECT_NAME)-$(basename $(basename $(notdir $(file)))) || true;)
	$(foreach file, $(wildcard ./infrastructure/environment/.generated/*.api.yml), docker container rm $(PROJECT_NAME)-$(basename $(basename $(notdir $(file)))) || true;)

.PHONY: start.api
start.api: build stop.api
	$(foreach file, $(wildcard ./infrastructure/environment/.generated/*.api.yml), $(eval API_PORT = `./.tools/jj -i ./.project.settings apis.$(basename $(basename $(notdir $(file)))).port`) docker run --name $(PROJECT_NAME)-$(basename $(basename $(notdir $(file)))) -d -v "$(CURDIR)/infrastructure/environment/.generated/$(notdir $(file)):/var/opt/template.yml" -v /var/run/docker.sock:/var/run/docker.sock -v "$(CURDIR):/var/opt" -p $(API_PORT):3000 $(PROJECT_NAME)/sam local start-api --docker-volume-basedir "$(CURDIR)" --host 0.0.0.0;)

.PHONY: stop.client
stop.client:
	$(foreach file, $(wildcard ./infrastructure/environment/.generated/*.client), docker stop $(PROJECT_NAME)-client-$(basename $(notdir $(file))) || true;)
	$(foreach file, $(wildcard ./infrastructure/environment/.generated/*.client), docker container rm $(PROJECT_NAME)-client-$(basename $(notdir $(file))) || true;)

.PHONY: start.client
start.client:
	$(foreach file, $(wildcard ./infrastructure/environment/.generated/*.client), $(eval CLIENT_PORT = `./.tools/jj -i $(file) port`) $(eval CLIENT_RELATIVE_PATH = `./.tools/jj -i $(file) relativePath`) docker run --name $(PROJECT_NAME)-client-$(basename $(notdir $(file))) -d -v "$(CURDIR)/$(CLIENT_RELATIVE_PATH):/usr/src/app" -p $(CLIENT_PORT):3000 $(PROJECT_NAME)/node-client run dev --hostname 0.0.0.0;)

.PHONY: start
start: start.localstack start.proxy start.api start.client ensure.infra ensure.localstack
	echo "Everything up and running."

.PHONY: stop
stop: stop.localstack stop.proxy stop.api
	echo "Everything stopped."
	
.PHONY: logs.localstack
logs.localstack:
	docker logs $(PROJECT_NAME)-localstack

.PHONY: logs.proxy
logs.proxy:
	$(foreach file, $(wildcard ./config/.generated/*.proxy), docker logs $(PROJECT_NAME)-proxy-$(basename $(notdir $(file)));)

.PHONY: logs.api
logs.api:
	$(foreach file, $(wildcard ./infrastructure/environment/.generated/*.api.yml), docker logs $(PROJECT_NAME)-$(basename $(basename $(notdir $(file))));)

.PHONY: logs.client
logs.client:
	$(foreach file, $(wildcard ./infrastructure/environment/.generated/*.client), docker logs $(PROJECT_NAME)-$(basename $(notdir $(file)));)

.PHONY: clear
clear: stop
	sudo rm -rf ./.localstack
