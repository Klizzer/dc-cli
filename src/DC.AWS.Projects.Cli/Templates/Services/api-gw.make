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
	$(foreach configFile, $(shell find . -iname 'api-gw.config.yml'), \
		$(eval API_NAME = `$(YQ_EXECUTABLE) r $(configFile) Name`) \
		$(eval API_PORT = `$(YQ_EXECUTABLE) r $(configFile) Settings.Port`) \
		$(eval API_PATH = $(dir $(configFile:./%=%))) \
		$(eval CONTAINER_NAME = "$(PROJECT_NAME)-api-$(API_NAME)") \
		docker run --name $(CONTAINER_NAME) -d \
			-v "$(CURDIR)/infrastructure/environment/.generated/$(API_NAME).api.yml:/usr/src/app/template.yml" \
			-v "/var/run/docker.sock:/var/run/docker.sock" \
			-v "$(CURDIR)/.env:/usr/src/app/.env" \
			-v "$(CURDIR)/$(API_PATH):/usr/src/app/$(API_PATH)" \
			-p $(API_PORT):3000 $(PROJECT_NAME)/sam local start-api \
				--env-vars ./.env/environment.variables.json \
				--docker-volume-basedir "$(CURDIR)/$(API_PATH)" \
				--host 0.0.0.0;)

.PHONY: stop
stop:
	$(foreach configFile, $(shell find . -iname 'api-gw.config.yml'), \
		$(eval API_NAME = `$(YQ_EXECUTABLE) r $(configFile) Name`) \
		$(eval CONTAINER_NAME = "$(PROJECT_NAME)-api-$(API_NAME)") \
		docker stop $(CONTAINER_NAME) || true; \
		docker container rm $(CONTAINER_NAME) || true;)

.PHONY: logs
logs:
	$(foreach configFile, $(shell find . -iname 'api-gw.config.yml'), \
		$(eval API_NAME = `$(YQ_EXECUTABLE) r $(configFile) Name`) \
		$(eval CONTAINER_NAME = "$(PROJECT_NAME)-api-$(API_NAME)") \
		docker logs $(CONTAINER_NAME);)