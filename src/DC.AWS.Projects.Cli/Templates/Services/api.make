SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

ifeq ($(OS), Windows_NT)
    JJ_EXECUTABLE := ./.tools/jj.exe
else
    JJ_EXECUTABLE := ./.tools/jj
endif

PROJECT_NAME ?=

API_NAME = [[API_NAME]]
CONTAINER_NAME = '$(PROJECT_NAME)-api-$(API_NAME)'

.PHONY: start
start: stop
	$(eval API_PORT = `$(JJ_EXECUTABLE) -i ./.project.settings apis.$(API_NAME).port`) 
	$(eval API_PATH = `$(JJ_EXECUTABLE) -i ./.project.settings apis.$(API_NAME).relativePath`) 
	docker run --name $(CONTAINER_NAME) -d \
		-v "$(CURDIR)/infrastructure/environment/.generated/$(API_NAME).api.yml:/usr/src/app/template.yml" \
		-v /var/run/docker.sock:/var/run/docker.sock \
		-v "$(CURDIR)/.env:/usr/src/app/.env" \
		-v "$(CURDIR)/$(API_PATH):/usr/src/app/$(API_PATH)" \
		-p $(API_PORT):3000 $(PROJECT_NAME)/sam local start-api \
			--env-vars ./.env/environment.variables.json \
			--docker-volume-basedir "$(CURDIR)/$(API_PATH)" \
			--host 0.0.0.0

.PHONY: stop
stop:
	docker stop $(CONTAINER_NAME) || true
	docker container rm $(CONTAINER_NAME) || true
	
.PHONY: logs
logs:
	docker logs $(CONTAINER_NAME)