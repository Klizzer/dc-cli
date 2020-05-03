SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

ifeq ($(OS), Windows_NT)
    DETECTED_OS := windows
else
    DETECTED_OS := $(shell sh -c 'uname | tr A-Z a-z 2>/dev/null || echo unknown')
endif

.PHONY: build
build:
	cd ./src && dotnet build
	
.PHONY: publish
publish:
	@make publish.$(DETECTED_OS)

.PHONY: publish.linux
publish.linux: clean
	dotnet publish ./src/DC.AWS.Projects.Cli -c Release --output $(CURDIR)/.out --self-contained -r linux-x64 -p:PublishSingleFile=true

.PHONY: publish.windows
publish.windows: clean
	dotnet publish ./src/DC.AWS.Projects.Cli -c Release --output $(CURDIR)/.out --self-contained -r win-x64 -p:PublishSingleFile=true

.PHONY: install
install:
	@make install.$(DETECTED_OS)

.PHONY: install.linux
install.linux: publish.linux
	sudo cp ./.out/dc-aws /usr/local/bin
	
.PHONY: clean
clean:
	rm -rf ./.out
	rm -rf ./**/**/obj
	rm -rf ./**/**/bin