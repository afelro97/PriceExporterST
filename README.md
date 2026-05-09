# Supermarket Together - Mod Exportador de Precios

Este es un mod para **Supermarket Together** creado con C# y BepInEx. Su función principal es aplicar ingeniería inversa para extraer los datos internos de los productos del juego (Costos, Inflación, Precios de Etiquetas y Tolerancia de NPCs) y exportarlos en tiempo real a una hoja de **Google Sheets** en la nube.

## ✨ Características

- **Exportación Matemática:** Extrae el ID, Nombre, Tier y Costo Base. Además, **calcula en tiempo real** el Precio de Mercado (Costo * Inflación del Tier), extrae el Precio de Etiqueta (Player Pricing) y descubre el límite máximo que los clientes pagarán (Threshold de NPC_Manager).
- **Sincronización en Tiempo Real:** El mod funciona como un vigilante de memoria. Detecta de forma automática cuando llega el día Jueves en el juego (cambio de inflación) o cuando el jugador modifica una etiqueta con la pistola de precios, enviando los cambios inmediatamente a la nube.
- **Modo Manual:** Puedes presionar la tecla **F9** en cualquier momento para forzar la exportación local a `.csv` y a la nube.

## 🛠️ Requisitos

- **Juego original:** Supermarket Together.
- **Cargador de Mods (Mod Loader):** [BepInEx 5.4.x](https://github.com/BepInEx/BepInEx/releases) instalado en la raíz del juego. Es indispensable para cargar y ejecutar el mod.
- **Entorno de compilación:** .NET SDK (solo necesario si vas a compilar el código fuente tú mismo).
- **Nube:** Una cuenta de Google libre y gratuita (para crear el puente con Google Apps Script).

## 🚀 Instalación y Uso

### 1. Preparar Google Sheets (El Puente)
1. Crea una nueva hoja de cálculo en Google Sheets.
2. Ve a **Extensiones > Apps Script**.
3. Pega tu código en JavaScript para sobreescribir la tabla.
4. Impleméntalo como **Aplicación Web** (dando acceso a *Cualquier persona*).
5. Copia la URL secreta que te proporciona Google.

### 2. Configurar el Mod
1. Ejecuta el juego una vez con el mod instalado para que BepInEx genere el archivo de configuración automáticamente, y luego cierra el juego.
2. Ve a la carpeta de tu juego y abre el archivo `BepInEx/config/com.tunombre.supermarket.precios.cfg` con cualquier editor de texto (como el Bloc de notas).
3. Pega la URL secreta que copiaste en el paso anterior justo después de `UrlGoogleSheets = ` y guarda el archivo.

### 3. Compilación
1. Abre una terminal en la carpeta de este proyecto (`ModExportadorPrecios`).
2. Ejecuta el siguiente comando:
   ```bash
   dotnet build
   ```
3. Gracias a un evento de post-compilación en `.csproj`, el archivo `ModExportadorPrecios.dll` se copiará automáticamente a tu carpeta `BepInEx/plugins/` del juego.

### 4. ¡A jugar!
Inicia tu partida. Verás en la consola negra de BepInEx los mensajes de "Sincronización automática de bienvenida...". 

Para probar la magia en vivo: apunta a cualquier producto en las estanterías con tu pistola de precios, cámbiale el valor y mira tu Google Sheet en un segundo monitor. ¡Los datos se actualizarán solos!

## 👨‍💻 Detalles Técnicos
Este mod fue desarrollado mediante la descompilación del `Assembly-CSharp.dll` del juego base, identificando:
* `ProductListing`: Base de datos que almacena `productsData`, `productPlayerPricing` y `tierInflation`.
* `NPC_Manager`: Administrador que alberga el arreglo `productsThreshholdArray` oculto.

---
*Creado con pasión, curiosidad e ingeniería de software.*
