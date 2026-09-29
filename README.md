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

Findit Agent acts as a smart archivist between your files and the AI:

1. **Privacy First:** The AI never gets direct or full access to your archive.
2. **On-Demand Access:** The AI asks Findit Agent to search, receives a list of results, and requests only specific documents or excerpts for deeper analysis.
3. **Local Sovereignty:** Only the text required for the current task is sent to an external AI provider. With a powerful machine and a local LLM (via Ollama), **the entire process stays 100% local** – your archive, searches, and analysis never leave your computer.

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

## ⚖️ License & Terms

**Freeware Beta / Source-available**  
Findit Agent Frontend and the underlying Findit engine are available **free of charge for private and experimental use**. Commercial, professional, or business use requires a valid commercial license.

👉 **[Full documentation and first steps](https://mhoub.github.io/Findit-Agent-Frontend/)**

