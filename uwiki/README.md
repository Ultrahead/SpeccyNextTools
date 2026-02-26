# ZX Basic Unofficial Wiki

A modern, fast, and fully offline-capable documentation portal for [ZX Basic](https://boriel.com/) (also known as Boriel Basic), created by Jose Rodriguez-Rosa ("Boriel"). 

This portal dynamically fetches, parses, and styles the markdown documentation files directly from the ZX Basic repository, providing an enhanced reading and searching experience.

---

## ⚠️ Unofficial Notice

**This is an unofficial wiki and is not affiliated with or maintained by the official ZX Basic development team.** For the most up-to-date and **official** documentation, please visit the official ReadTheDocs page:  
👉 **[https://zxbasic.readthedocs.io/](https://zxbasic.readthedocs.io/)**

---

## ✨ Features

* **Instant Search:** Live filtering of all basic statements, libraries, and architectures.
* **100% Offline Mode:** Use the built-in "💾 Offline" button or the included PowerShell script to bundle the entire wiki (including images and markdown) into a single, double-clickable HTML file using a secure JSON Island architecture.
* **Smart Print & PDF Export:** Click "📄 Full Manual" to automatically compile every document into a beautifully formatted, book-ready layout with page breaks and a table of contents.
* **Syntax Highlighting:** Full syntax highlighting for ZX Basic and embedded Assembly code.
* **Dark & Light Themes:** Toggleable UI themes that respect standard system colors.

## 🚀 How to Use

### Online / Live Web Version
Simply open `index.html` in any modern web browser. The application will automatically reach out to the GitHub repository, build the navigation tree, and parse the latest Markdown files on the fly.

### Generating the Offline Version
If you want to take the documentation on the go without an internet connection:
1. Open the live version and click the **"💾 Offline"** button in the top menu.
2. **OR**, run the included `GenerateOffline.ps1` PowerShell script from your terminal. 

Both methods will scrape the latest documentation, embed it directly into the HTML file, and stamp it with the latest commit date.

## 🙌 Credits
* **ZX Basic** was created and is maintained by Jose Rodriguez-Rosa ("Boriel").
* The documentation text rendered in this portal belongs to the original contributors of the ZX Basic project.