# Findit6Agent source code

This folder contains the source code of **Findit6Agent**.

## Technology

Findit6Agent is written in **VB.NET** and targets **.NET 8 for Windows**.

The project can be opened and compiled with a current version of **Microsoft Visual Studio** with .NET desktop development support installed.

## What is included

This folder contains the Visual Studio solution/project files and the source files required to compile Findit6Agent.

## What is not included

The runtime environment is intentionally not part of this source folder.

A compiled build additionally requires several runtime components in the expected subdirectories, including:

- the AgentWorkspace files
- the semantic reranker
- the dedicated Python runtime
- the reranker model files
- additional runtime resources used by the application

These components are included in the binary installer available in the Releases section.

## Important

Existing Python installations on the system are not required and are not modified by Findit6Agent.

For normal use, please use the complete installer from the project Releases page.
