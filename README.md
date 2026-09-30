# Findit Agent 🕵️‍♂️
> **Agentic AI research for your local document collections – without the RAG overhead.**

Findit Agent brings advanced agentic AI research directly to your local files. It can find a single fact hidden inside thousands of files or research complex topics across multiple documents to generate an evidence-based report. 

The research is performed by an AI agent using a wide range of supported LLMs (cloud providers or completely local models via Ollama). Instead of requiring a prepared AI knowledge base, Findit Agent uses **Findit6** as a "grep on steroids":

* 🚫 **No indexing** required
* 🚫 **No vectorization** or embeddings required
* 🚫 **No vector database** setup needed
* 🚫 **No preprocessing** of your archives
* ⚡ **Immediate search** for newly added or changed files

---

## 🏛️ The Archivist Principle

Findit6Agent acts as a strict **smart archivist** between your files and the AI.

* **Privacy First:** The AI never gets any kind of access to any file in your archive. It operates completely isolated from your file system.
* **On-Demand Access:** The AI asks Findit6Agent to search and receives only a list of results. It can then request specific documents or excerpts for deeper analysis—however, **the AI never accesses the original files directly**. It only receives extracted text copies transmitted strictly in **UTF-8 format**.
* **Local Sovereignty:** Only the specific text required for the current task is passed to an AI provider. With a sufficiently powerful computer and a local LLM (via Ollama), **the entire process remains 100% local**: your archive, searches, extracted text, and AI analysis never leave your machine.

👉 **[Full documentation and first steps](https://mhoub.github.io/Findit-Agent-Frontend/)**

---

## 🚀 Quick Start (Windows only)

### 1. Download & Install
👉 **[Download the complete Windows installer](https://github.com/MHoub/Findit-Agent-Frontend/releases/tag/v1.00.01)**  
*The installer contains all required components, including the local reranker and its runtime environment. No manual setup required.*

### 2. Prerequisites
You only need one of the following:
* An **API key** for a supported cloud AI provider (e.g., OpenAI, Anthropic, Gemini)
* A working **Ollama** installation for 100% local execution

---

## 💡 Why Findit Agent?

Traditional AI document assistants require time-consuming indexing and vector databases. Findit Agent queries your existing local archives directly. It supports **Office documents, PDFs, emails, ZIP/tar archives, source code, and OCR-processed documents**. 

The AI agent uses these direct search results to:
* **Find individual facts** ("needle in a haystack")
* **Compare** multiple relevant documents
* **Identify relationships** and hidden patterns
* **Synthesize evidence** into a comprehensive, source-based research report

---

## 🔍 Fully Traceable & Verifiable Research

No more AI hallucinations without proof. Findit Agent is designed to make every step of the research process transparent:

* **Direct Citations:** Every factual statement in the final report is linked directly to the original source file. Open it with a single click.
* **Complete Audit Trail:** The tool logs every single search query performed by the agent, every result list generated, every excerpt the agent actually read, and the exact conclusions drawn.

You can reopen and inspect the agent's search history at any time, making the results **reproducible, transparent, and directly verifiable**.

---

## 📸 Screenshot & Example

![Findit Agent Screenshot](images/FINDIT6AGENT-Results.png)

*Example: A short "needle in a haystack" lookup performed by Gemini Flash. The agent automatically works in the language used by the user and dominant in the archive. In this case, it successfully located a hidden private mobile number inside a large archive.*

---

## ⚖️ Source-Available & License Terms

This repository contains the **complete source code** for Findit6Agent. 

* **Source-Available:** The source code is licensed under the **[PolyForm Perimeter License 1.0.1](https://polyformproject.org)**. You are free to inspect, study, and modify the code, but you may not use it to create or support a competing product or service.
* **Private & Experimental Use:** Findit6Agent is **free of charge** for private, non-commercial experimental, and evaluation use without a fixed expiration date.
* **Commercial Use:** Any commercial, professional, or business use requires a valid commercial **Findit6** license (which includes Findit6Agent).

*Note: This project is source-available, but not Open Source in the OSI-approved sense due to the competition restriction.*

👉 For full details, warranty limitations, and third-party notices, please see the complete **[License & Legal Documentation](https://mhoub.github.io/Findit-Agent-Frontend/Findit6Agent_License_and_Legal.html)**.


👉 **[Full documentation and first steps](https://mhoub.github.io/Findit-Agent-Frontend/)**

