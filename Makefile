SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

ifeq ($(OS), Windows_NT)
    DETECTED_OS := Windows
else
    DETECTED_OS := $(shell sh -c 'uname 2>/dev/null || echo Unknown')
endif

.PHONY: build
build:
	cd ./src && dotnet build
	
.PHONY: publish
publish: clean
ifeq (DETECTED_OS, Windows)
	dotnet publish ./src/DC.AWS.Projects.Cli -c Release --output $(CURDIR)/.out --self-contained -r win-x64 -p:PublishSingleFile=true
else
	dotnet publish ./src/DC.AWS.Projects.Cli -c Release --output $(CURDIR)/.out --self-contained -r linux-x64 -p:PublishSingleFile=true
endif

.PHONY: install
install: publish
ifeq (DETECTED_OS, Windows)
else
	sudo cp ./.out/dc-aws /usr/local/bin
endif

.PHONY: clean
clean:
	rm -rf ./.out
	rm -rf ./**/**/obj
	rm -rf ./**/**/bin