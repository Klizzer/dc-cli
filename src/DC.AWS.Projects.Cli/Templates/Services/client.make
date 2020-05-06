SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

ifeq ($(OS), Windows_NT)
	YQ_EXECUTABLE := ./.tools/yq.exe
else
    YQ_EXECUTABLE := ./.tools/yq
endif

PROJECT_NAME ?=

.PHONY: start
start: stop
	$(foreach configFile, $(shell find . -iname 'client.config.yml'), \
		$(eval CLIENT_NAME = `$(YQ_EXECUTABLE) r $(configFile) Name`) \
		$(eval CLIENT_PORT = `$(YQ_EXECUTABLE) r $(configFile) Settings.Port`) \
		$(eval CLIENT_PATH = $(dir $(configFile:./%=%))) \
		$(eval CONTAINER_NAME = "$(PROJECT_NAME)-client-$(CLIENT_NAME)") \
		docker run --name $(CONTAINER_NAME) -d \
			-v "$(CURDIR)/$(CLIENT_PATH):/usr/src/app" \
			-p $(CLIENT_PORT):3000 $(PROJECT_NAME)/node-client run dev --hostname 0.0.0.0;)

.PHONY: stop
stop:
	$(foreach configFile, $(shell find . -iname 'api-gw.config.yml'), \
		$(eval CLIENT_NAME = `$(YQ_EXECUTABLE) r $(configFile) Name`) \
		$(eval CONTAINER_NAME = "$(PROJECT_NAME)-client-$(CLIENT_NAME)") \
		docker stop $(CONTAINER_NAME) || true; \
		docker container rm $(CONTAINER_NAME) || true;)
	
.PHONY: logs
logs:
	$(foreach configFile, $(shell find . -iname 'api-gw.config.yml'), \
		$(eval CLIENT_NAME = `$(YQ_EXECUTABLE) r $(configFile) Name`) \
		$(eval CONTAINER_NAME = "$(PROJECT_NAME)-client-$(CLIENT_NAME)") \
		docker logs $(CONTAINER_NAME);)