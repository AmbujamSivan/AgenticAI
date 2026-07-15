# Agentic AI Portfolio 🤖🚀

Welcome to my Agentic AI laboratory. This repository is a monorepo containing production-grade, autonomous, and multi-agent systems. Each project is self-contained and showcases a different architectural pattern of modern AI engineering (such as Swarms, RAG, tool-use, and MCP integration).

---

## 🛠️ Tech Stack & Concepts
* **Languages:** C# (.NET Core), Python
* **Frameworks/Orchestration:** Semantic Kernel, LangGraph, AutoGen
* **Core Concepts:** Multi-agent collaboration, Model Context Protocol (MCP), Function Calling, Vector Databases

---

## 📂 Featured Projects

### 🎥 1. Multi-Agent YouTube Automation Channel
An autonomous multi-agent system designed to plan, script, and monitor a faceless YouTube channel focused on high-quality race car sounds.
* **Architecture:** Supervisor-Worker Pattern (Content Planner Agent, Audio Compiler Agent, SEO Metadata Agent).
* **Core Tech:** C#, Semantic Kernel, Custom MCP servers for media storage.
* **[Explore Project Directory 📂](./projects/01-multi-agent-youtube)** | **[Architecture Design 📊](./projects/01-multi-agent-youtube#architecture)**

### 📈 2. Real-Time Financial Swarm
A swarm of cooperative agents analyzing real-time market data, sentiment, and technical indicators to compile investment-ready research briefs.
* **Architecture:** Decentralized Choreography.
* **Core Tech:** Python, LangGraph, Qdrant Vector DB, Tavily Search API.
* **[Explore Project Directory 📂](./projects/02-financial-analyst-swarm)**

---

## ⚙️ How to Run
Most projects use Docker Compose for easy evaluation. Check the individual project directories for specific environment variable setups (.env).
