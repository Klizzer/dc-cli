SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

PROJECT_NAME ?=

ifeq ($(OS), Windows_NT)
	YQ_EXECUTABLE := ./.tools/yq.exe
else
    YQ_EXECUTABLE := ./.tools/yq
endif

.PHONY: start
start: stop
	$(foreach configFile, $(shell find . -iname 'proxy.config.yml'), \
		$(eval PROXY_NAME = `$(YQ_EXECUTABLE) r $(configFile) Name`) \
		$(eval PROXY_PORT = `$(YQ_EXECUTABLE) r $(configFile) Settings.Port`) \
		$(eval PROXY_PATH = $(dir $(configFile:./%=%))) \
		$(eval CONTAINER_NAME = "$(PROJECT_NAME)-proxy-$(PROXY_NAME)") \
		docker run --name $(CONTAINER_NAME) -d \
			-v "$(CURDIR)/$(PROXY_PATH)/proxy.nginx.conf:/etc/nginx/nginx.conf" \
			-v "$(CURDIR)/$(PROXY_PATH)/_paths:/etc/nginx/_paths" \
			-p $(PROXY_PORT):80 nginx;)

.PHONY: stop
stop:
	$(foreach configFile, $(shell find . -iname 'proxy.config.yml'), \
		$(eval PROXY_NAME = `$(YQ_EXECUTABLE) r $(configFile) Name`) \
		$(eval CONTAINER_NAME = "$(PROJECT_NAME)-proxy-$(PROXY_NAME)") \
		docker stop $(CONTAINER_NAME) || true; \
		docker container rm $(CONTAINER_NAME) || true;)

.PHONY: logs
logs:
	$(foreach configFile, $(shell find . -iname 'proxy.config.yml'), \
		$(eval PROXY_NAME = `$(YQ_EXECUTABLE) r $(configFile) Name`) \
		$(eval CONTAINER_NAME = "$(PROJECT_NAME)-proxy-$(PROXY_NAME)") \
		docker logs $(CONTAINER_NAME);)