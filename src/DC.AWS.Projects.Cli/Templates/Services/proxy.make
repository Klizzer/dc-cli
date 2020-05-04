SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

PROJECT_NAME ?=

PROXY_NAME = [[PROXY_NAME]]
CONFIG_PATH = [[CONFIG_PATH]]
PORT = [[PORT]]
UPSTREAM_PORT = [[UPSTREAM_PORT]]
CONTAINER_NAME = $(PROJECT_NAME)-proxy-$(PROXY_NAME)

ifeq ($(OS), Windows_NT)
    DETECTED_OS := Windows
else
    DETECTED_OS := $(shell sh -c 'uname 2>/dev/null || echo Unknown')
endif

.PHONY: start
start: stop
ifeq(DETECTED_OS, Windows)
	if not exists "$(CURDIR)/.generated" mkdir "$(CURDIR)/.generated"
	if not exists "$(CURDIR)/$(CONFIG_PATH)/_child_paths" mkdir "$(CURDIR)/$(CONFIG_PATH)/_child_paths"
else
	mkdir -p "$(CURDIR)/.generated"
	mkdir -p "$(CURDIR)/$(CONFIG_PATH)/_child_paths"
endif
ifeq (DETECTED_OS, Linux)
	$(eval LOCAL_IP=`ip route get 8.8.8.8 | sed -n '/src/{s/.*src *\([^ ]*\).*/\1/p;q}'`)
	echo "upstream local-upstream { least_conn; server $(LOCAL_IP):$(UPSTREAM_PORT); }" >> "$(CURDIR)/.generated/$(CONTAINER_NAME)-upstream.conf"
else
	echo "upstream local-upstream { least_conn; server host.docker.internal:$(UPSTREAM_PORT); }" >> "$(CURDIR)/.generated/$(CONTAINER_NAME)-upstream.conf"
endif
	docker pull nginx
	docker run --name $(CONTAINER_NAME) -d \
		-v "$(CURDIR)/$(CONFIG_PATH)/proxy.nginx.conf:/etc/nginx/nginx.conf" \
		-v "$(CURDIR)/$(CONFIG_PATH)/_child_paths:/etc/nginx/_child_paths" \
		-v "$(CURDIR)/.generated/$(CONTAINER_NAME)-upstream.conf:/etc/nginx/local-upstream.conf" \
		-p $(PORT):80 nginx

.PHONY: stop
stop:
	docker stop $(CONTAINER_NAME) || true
	docker container rm $(CONTAINER_NAME) || true
	
.PHONY: logs
logs:
	docker logs $(CONTAINER_NAME)