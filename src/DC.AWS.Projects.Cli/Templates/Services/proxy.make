SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

PROJECT_NAME ?=

PROXY_NAME = [[PROXY_NAME]]
CONFIG_PATH = [[CONFIG_PATH]] 
PORT = [[PORT]]
CONTAINER_NAME = '$(PROJECT_NAME)-proxy-$(PROXY_NAME)'

.PHONY: start
start: stop
	docker pull nginx
	docker run --name $(CONTAINER_NAME) -d \
		-v "$(CURDIR)/$(CONFIG_PATH)/proxy.nginx.conf:/etc/nginx/nginx.conf" \
		-v "$(CURDIR)/$(CONFIG_PATH)/_child_paths:/etc/nginx/_child_paths" \
		-p $(PORT):80 nginx

.PHONY: stop
stop:
	docker stop $(CONTAINER_NAME) || true
	docker container rm $(CONTAINER_NAME) || true
	
.PHONY: logs
logs:
	docker logs $(CONTAINER_NAME)