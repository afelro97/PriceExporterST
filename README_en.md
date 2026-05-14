*Lea esto en otros idiomas: [Español](README.md).*

# Supermarket Together - Price Exporter Mod

This is a mod for **Supermarket Together** created with C# and BepInEx. Its main function is to apply reverse engineering to extract the internal product data of the game (Costs, Inflation, Label Prices, and NPC Tolerance) and export them in real-time to a **Google Sheets** spreadsheet in the cloud.

## ✨ Features

- **Mathematical Export:** Extracts ID, Name, Tier, and Base Cost. Additionally, **calculates in real-time** the Market Price (Cost * Tier Inflation), extracts the Label Price (Player Pricing), and discovers the maximum limit customers will pay (NPC_Manager Threshold).
- **Real-Time Synchronization:** The mod acts as a memory watcher. It automatically detects when Thursday arrives in the game (inflation change) or when the player modifies a label with the price gun, sending the changes immediately to the cloud.
- **Manual Mode:** You can press the **F9** key at any time, or use the **export button** within the BepInEx menu (F1) to force synchronization.
- **Multilingual:** Includes native support for English and Spanish (`lang_en.txt` and `lang_es.txt`). The language can be easily changed from the configuration menu (F1).

## 🛠️ Requirements

- **Base game:** Supermarket Together.
- **Mod Loader:** BepInEx 5.4.x installed in the game's root folder. It is essential to load and run the mod.
- **Build environment:** .NET SDK (only necessary if you are going to compile the source code yourself).
- **Cloud:** A free Google account (to create the bridge with Google Apps Script).

## 🚀 Installation and Usage

### 1. Prepare Google Sheets (The Bridge)
1. Create a new spreadsheet in Google Sheets.
2. Go to **Extensions > Apps Script**.
3. Delete any existing code and paste this JavaScript code to overwrite the table:
   ```javascript
   function doPost(e) {
     try {
       var sheet = SpreadsheetApp.getActiveSpreadsheet().getActiveSheet();
       var csvData = Utilities.parseCsv(e.postData.contents);
       sheet.clearContents();
       sheet.getRange(1, 1, csvData.length, csvData.length).setValues(csvData);
       return ContentService.createTextOutput("OK");
     } catch(error) {
       return ContentService.createTextOutput("Error: " + error);
     }
   }
   ```
4. Deploy it as a **Web App** (giving access to *Anyone*).
5. Copy the secret URL provided by Google.

### 2. Configure the Mod
1. **(Recommended)** Make sure you have **BepInEx Configuration Manager** installed in your plugins folder.
2. Start the game and press **F1** to open the overlay configuration menu.
3. Find and expand the **Exportador de Precios CSV** tab, paste your secret URL in the corresponding text box, and that's it! It will save automatically.
   *(In this same menu, you will find an "Export to Google Sheets" button to force an instant upload).*
*(Manual alternative without the menu: Run the game once, close it, and paste your URL in the `BepInEx/config/com.tunombre.supermarket.precios.cfg` file).*

### 3. Compilation
1. Open a terminal in this project's folder (`ModExportadorPrecios`).
2. Run the following command:
   ```bash
   dotnet build
   ```
3. Thanks to a post-build event in `.csproj`, the `ModExportadorPrecios.dll` file will automatically be copied to your game's `BepInEx/plugins/` folder.

### 4. Let's play!
Start your game. You will see the "Sincronización automática de bienvenida..." (Automatic welcome synchronization) messages in the black BepInEx console. 

To test the magic live: aim at any product on the shelves with your price gun, change the value, and look at your Google Sheet on a second monitor. The data will update by itself!

## 👨‍💻 Technical Details
This mod was developed by decompiling the base game's `Assembly-CSharp.dll`, identifying:
* `ProductListing`: Database that stores `productsData`, `productPlayerPricing`, and `tierInflation`.
* `NPC_Manager`: Manager that hosts the hidden `productsThreshholdArray` array.

## 🐛 Bugs and Feedback

If you have any ideas to improve the mod, want to add a new language, or found a bug, I'd love to hear it!
Please open a ticket in the **Issues** tab of this repository: Create a new Issue

---
*Created with passion, curiosity, and software engineering.*