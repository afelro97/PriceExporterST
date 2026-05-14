﻿using System;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace ModExportadorPrecios
{
    [BepInPlugin("com.tunombre.supermarket.precios", "Exportador de Precios CSV", "1.0.0")]
    public class ExportadorPlugin : BaseUnityPlugin
    {
        public static ExportadorPlugin Instancia;
        private double hashEstadoAnterior = -1;
        private float timer = 0f;
        private BepInEx.Configuration.ConfigEntry<string> configUrlGoogle;
        private BepInEx.Configuration.ConfigEntry<string> configIdioma;
        private static Dictionary<string, string> textos = new Dictionary<string, string>();
        
        private ConfigurationManagerAttributes attrMenuIdioma;
        private ConfigurationManagerAttributes attrMenuUrl;
        private ConfigurationManagerAttributes attrMenuForzar;

        // Clase auxiliar para personalizar el menú de BepInEx (DEBE llamarse exactamente así)
        public sealed class ConfigurationManagerAttributes
        {
            public Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;
            public string DispName;
            public bool HideSettingName; // ¡Nueva variable para ocultar el texto congelado de BepInEx!
        }

        static void DibujarBoton(BepInEx.Configuration.ConfigEntryBase entry)
        {
            GUILayout.BeginVertical();
            
            // Título con ajuste de línea automático
            GUIStyle estiloLabel = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Label(Traducir("MenuForzar", "Sincronización Manual"), estiloLabel);
            
            if (GUILayout.Button(Traducir("BotonExportar", "Exportar a Google Sheets"), GUILayout.ExpandWidth(true)))
            {
                if (Instancia != null) Instancia.ExportarCSVReal();
            }
            GUILayout.EndVertical();
        }

        static void DibujarUrl(BepInEx.Configuration.ConfigEntryBase entry)
        {
            GUILayout.BeginVertical();
            
            GUIStyle estiloLabel = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Label(Traducir("MenuUrl", "URL de Google Sheets"), estiloLabel);
            
            // Usamos TextArea multi-línea para que el enlace largo se acomode sin empujar la pantalla
            GUIStyle estiloTexto = new GUIStyle(GUI.skin.textArea) { wordWrap = true };
            entry.BoxedValue = GUILayout.TextArea(entry.BoxedValue?.ToString() ?? "", estiloTexto, GUILayout.ExpandWidth(true));
            GUILayout.EndVertical();
        }

        public static string Traducir(string clave, string porDefecto)
        {
            // Busca si existe la traducción, si no, devuelve el texto por defecto
            if (textos.ContainsKey(clave)) return textos[clave];
            return porDefecto;
        }

        void GenerarPlantillasDeIdioma()
        {
            string pathEs = Path.Combine(Paths.PluginPath, "lang_es.txt");
            if (!File.Exists(pathEs))
            {
                StringBuilder es = new StringBuilder();
                es.AppendLine("# Archivo de traducción del mod Exportador de Precios");
                es.AppendLine("LogCargado=¡El mod Exportador de Precios se ha cargado correctamente!");
                es.AppendLine("BotonExportar=Exportar a Google Sheets");
                es.AppendLine("DescUrl=Pega aquí tu URL de Google Apps Script para sincronizar con la nube.");
                es.AppendLine("DescForzar=Haz clic en el botón para enviar los datos manualmente.");
                es.AppendLine("EnviandoNube=Enviando datos a la nube de Google Sheets...");
                es.AppendLine("ExitoNube=¡Google Sheets actualizado con éxito!");
                es.AppendLine("SyncBienvenida=Tienda iniciada. Sincronización automática de bienvenida...");
                es.AppendLine("SyncCambio=¡CAMBIO DE MERCADO O ETIQUETA DETECTADO! Sincronizando con Google Sheets...");
                es.AppendLine("ExportandoLog=--- EXPORTANDO DATOS FINALES (MERCADO, ETIQUETAS Y UMBRALES) ---");
                es.AppendLine("ErrorCatalogo=No se encontró el catálogo en escena. ¿Estás dentro de la tienda?");
                es.AppendLine("ExitoCSV=¡Éxito! CSV guardado con {0} productos reales en: {1}");
                es.AppendLine("ErrorGoogle=Error al conectar con Google Sheets: ");
                es.AppendLine("ErrorClase=Error: No se pudo localizar la clase ProductListing.");
                es.AppendLine("MenuUrl=URL de Google Sheets");
                es.AppendLine("MenuForzar=Sincronización Manual");
                es.AppendLine("MenuIdioma=Idioma");
                File.WriteAllText(pathEs, es.ToString());
            }

            string pathEn = Path.Combine(Paths.PluginPath, "lang_en.txt");
            if (!File.Exists(pathEn))
            {
                StringBuilder en = new StringBuilder();
                en.AppendLine("# Translation file for Price Exporter Mod");
                en.AppendLine("LogCargado=Price Exporter mod loaded successfully!");
                en.AppendLine("BotonExportar=Export to Google Sheets");
                en.AppendLine("DescUrl=Paste your Google Apps Script URL here to sync with the cloud.");
                en.AppendLine("DescForzar=Click the button to manually send the data.");
                en.AppendLine("EnviandoNube=Sending data to Google Sheets cloud...");
                en.AppendLine("ExitoNube=Google Sheets updated successfully!");
                en.AppendLine("SyncBienvenida=Store started. Automatic welcome synchronization...");
                en.AppendLine("SyncCambio=MARKET OR LABEL CHANGE DETECTED! Syncing with Google Sheets...");
                en.AppendLine("ExportandoLog=--- EXPORTING FINAL DATA (MARKET, LABELS AND THRESHOLDS) ---");
                en.AppendLine("ErrorCatalogo=Catalog not found in scene. Are you inside the store?");
                en.AppendLine("ExitoCSV=Success! CSV saved with {0} real products in: {1}");
                en.AppendLine("ErrorGoogle=Error connecting to Google Sheets: ");
                en.AppendLine("ErrorClase=Error: Could not locate ProductListing class.");
                en.AppendLine("MenuUrl=Google Sheets URL");
                en.AppendLine("MenuForzar=Manual Synchronization");
                en.AppendLine("MenuIdioma=Language");
                File.WriteAllText(pathEn, en.ToString());
            }
        }

        void ConfigurarIdioma()
        {
            GenerarPlantillasDeIdioma();

            attrMenuIdioma = new ConfigurationManagerAttributes { DispName = "Idioma / Language" }; // Lo dejamos global
            configIdioma = Config.Bind("General", "Idioma", "es", new ConfigDescription("Código del idioma / Language code. El mod buscará el archivo lang_[idioma].txt en la carpeta plugins.", new AcceptableValueList<string>("es", "en"), attrMenuIdioma));
            string archivoIdioma = Path.Combine(Paths.PluginPath, $"lang_{configIdioma.Value}.txt");

            if (File.Exists(archivoIdioma))
            {
                // Leer el archivo de texto y cargar el diccionario
                foreach (string linea in File.ReadAllLines(archivoIdioma))
                {
                    if (!string.IsNullOrWhiteSpace(linea) && linea.Contains("=") && !linea.StartsWith("#"))
                    {
                        var partes = linea.Split(new[] { '=' }, 2);
                        textos[partes[0].Trim()] = partes[1].Trim();
                    }
                }
            }
            else
            {
                Logger.LogWarning($"El archivo de idioma {archivoIdioma} no existe. Usando textos por defecto (Español).");
            }
        }

        void Awake()
        {
            Instancia = this;
            ConfigurarIdioma(); // ¡Muy importante cargar esto primero!
            
            Logger.LogInfo(Traducir("LogCargado", "¡El mod Exportador de Precios se ha cargado correctamente!"));
            
            attrMenuUrl = new ConfigurationManagerAttributes { HideSettingName = true, CustomDrawer = DibujarUrl };
            // Configuramos BepInEx para que lea la URL desde un archivo externo
            configUrlGoogle = Config.Bind("Nube", 
                                          "UrlGoogleSheets", 
                                          "", // El valor por defecto estará vacío
                                          new ConfigDescription(Traducir("DescUrl", "Pega aquí tu URL de Google Apps Script para sincronizar con la nube."), null, attrMenuUrl));

            attrMenuForzar = new ConfigurationManagerAttributes { HideSettingName = true, CustomDrawer = DibujarBoton };
            // Agregamos el botón a la ventana de configuración
            Config.Bind("Nube", 
                        "Forzar Sincronización", 
                        "", 
                        new ConfigDescription(Traducir("DescForzar", "Haz clic en el botón para enviar los datos manualmente."), null, attrMenuForzar));
                        
            // ¡MAGIA!: Evento que se dispara al cambiar el idioma
            configIdioma.SettingChanged += (sender, args) => RecargarIdioma();
        }

        void RecargarIdioma()
        {
            textos.Clear();
            string archivoIdioma = Path.Combine(Paths.PluginPath, $"lang_{configIdioma.Value}.txt");
            
            if (File.Exists(archivoIdioma))
            {
                foreach (string linea in File.ReadAllLines(archivoIdioma))
                {
                    if (!string.IsNullOrWhiteSpace(linea) && linea.Contains("=") && !linea.StartsWith("#"))
                    {
                        var partes = linea.Split(new[] { '=' }, 2);
                        textos[partes[0].Trim()] = partes[1].Trim();
                    }
                }
            }
            
            Logger.LogInfo($"Idioma actualizado dinámicamente a: {configIdioma.Value}");
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                ExportarCSVReal();
            }
            
            // Si presiona F10, buscamos el precio de venta que nos falta
            if (Input.GetKeyDown(KeyCode.F10))
            {
                BuscarPreciosDeVenta();
            }
            
            // Auto-vigilante cada 5 segundos
            timer += Time.deltaTime;
            if (timer >= 5f)
            {
                timer = 0f;
                VigilarCambios();
            }
        }

        void VigilarCambios()
        {
            Type tipoCatálogo = Assembly.Load("Assembly-CSharp").GetType("ProductListing");
            if (tipoCatálogo == null) return;
            
            UnityEngine.Object catalogoObj = UnityEngine.Object.FindFirstObjectByType(tipoCatálogo);
            if (catalogoObj == null) return;
            
            FieldInfo inflationField = tipoCatálogo.GetField("tierInflation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo playerPricingField = tipoCatálogo.GetField("productPlayerPricing", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            float[] arrayInflacion = inflationField?.GetValue(catalogoObj) as float[];
            float[] arrayEtiquetas = playerPricingField?.GetValue(catalogoObj) as float[];
            
            double estadoActual = 0;
            
            // Creamos una "huella digital" matemática sumando todos los precios de la tienda
            if (arrayInflacion != null)
            {
                for (int i = 0; i < arrayInflacion.Length; i++) 
                    estadoActual += arrayInflacion[i] * (i + 1); // Multiplicamos por el índice para que sea único
            }
            if (arrayEtiquetas != null)
            {
                for (int i = 0; i < arrayEtiquetas.Length; i++) 
                    estadoActual += arrayEtiquetas[i] * (i + 100);
            }
            
            // Si es la primera vez que lee (juego recién cargado y tienda abierta)
            if (hashEstadoAnterior == -1)
            {
                hashEstadoAnterior = estadoActual;
                Logger.LogInfo(Traducir("SyncBienvenida", "Tienda iniciada. Sincronización automática de bienvenida..."));
                ExportarCSVReal();
            }
            // Si la huella digital cambió (llegó el Jueves y cambió la inflación, O tú cambiaste una etiqueta)
            else if (Math.Abs(hashEstadoAnterior - estadoActual) > 0.001)
            {
                hashEstadoAnterior = estadoActual;
                Logger.LogInfo(Traducir("SyncCambio", "¡CAMBIO DE MERCADO O ETIQUETA DETECTADO! Sincronizando con Google Sheets..."));
                ExportarCSVReal();
            }
        }

        public void ExportarCSVReal() // <-- ¡Importante hacerlo public para que el botón pueda acceder!
        {
            Logger.LogInfo(Traducir("ExportandoLog", "--- EXPORTANDO DATOS FINALES (MERCADO, ETIQUETAS Y UMBRALES) ---"));
            string rutaArchivo = Path.Combine(Paths.PluginPath, "PreciosExportados.csv");
            StringBuilder csv = new StringBuilder();
            
            Type tipoCatálogo = Assembly.Load("Assembly-CSharp").GetType("ProductListing");
            Type tipoNPCManager = Assembly.Load("Assembly-CSharp").GetType("NPC_Manager");
            
            if (tipoCatálogo != null)
            {
                UnityEngine.Object catalogoObj = UnityEngine.Object.FindFirstObjectByType(tipoCatálogo);
                UnityEngine.Object npcManagerObj = tipoNPCManager != null ? UnityEngine.Object.FindFirstObjectByType(tipoNPCManager) : null;
                
                if (catalogoObj == null)
                {
                    Logger.LogWarning(Traducir("ErrorCatalogo", "No se encontró el catálogo en escena. ¿Estás dentro de la tienda?"));
                    return;
                }
                
                // Nuestras nuevas cabeceras maestras
                csv.AppendLine("ID_Producto,Nombre,Tier,Costo_Base,Precio_Mercado,Precio_Etiqueta,Max_Precio_NPC");
                
                FieldInfo listaField = tipoCatálogo.GetField("productsData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo playerPricingField = tipoCatálogo.GetField("productPlayerPricing", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo inflationField = tipoCatálogo.GetField("tierInflation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                float[] arrayEtiquetas = playerPricingField?.GetValue(catalogoObj) as float[];
                float[] arrayInflacion = inflationField?.GetValue(catalogoObj) as float[];
                
                float[] arrayThreshold = null;
                if (npcManagerObj != null)
                {
                    FieldInfo thresholdField = tipoNPCManager.GetField("productsThreshholdArray", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    arrayThreshold = thresholdField?.GetValue(npcManagerObj) as float[];
                }
                
                System.Collections.IEnumerable listaProductos = listaField.GetValue(catalogoObj) as System.Collections.IEnumerable;
                
                int contador = 0;
                foreach (var producto in listaProductos)
                {
                    Type tipoProducto = producto.GetType();
                    FieldInfo idField = tipoProducto.GetField("productID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    FieldInfo nameField = tipoProducto.GetField("productBrand", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    FieldInfo priceField = tipoProducto.GetField("basePricePerUnit", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    FieldInfo tierField = tipoProducto.GetField("productTier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    int idInt = (int)(idField?.GetValue(producto) ?? 0);
                    int tierInt = (int)(tierField?.GetValue(producto) ?? 0);
                    
                    string nombre = nameField?.GetValue(producto)?.ToString().Replace(",", "") ?? "Desconocido";
                    float costoBase = (float)(priceField?.GetValue(producto) ?? 0f);
                    
                    // ¡LA MAGIA MATEMÁTICA! Precio de Mercado = Costo Base * Inflación del Tier
                    float inflacion = (arrayInflacion != null && tierInt >= 0 && tierInt < arrayInflacion.Length) ? arrayInflacion[tierInt] : 1f;
                    float precioMercado = (float)Math.Round(costoBase * inflacion, 2);
                    
                    // Precio que le puso el jugador a la etiqueta
                    float precioEtiqueta = (arrayEtiquetas != null && idInt >= 0 && idInt < arrayEtiquetas.Length) ? arrayEtiquetas[idInt] : 0f;
                    
                    // Límite máximo que un cliente pagará por este producto
                    float precioNPC = (arrayThreshold != null && idInt >= 0 && idInt < arrayThreshold.Length) ? arrayThreshold[idInt] : 0f;
                    
                    // Formateamos los números a 2 decimales limpios
                    string fCosto = string.Format(CultureInfo.InvariantCulture, "{0:0.00}", costoBase);
                    string fMercado = string.Format(CultureInfo.InvariantCulture, "{0:0.00}", precioMercado);
                    string fEtiqueta = string.Format(CultureInfo.InvariantCulture, "{0:0.00}", precioEtiqueta);
                    string fNPC = string.Format(CultureInfo.InvariantCulture, "{0:0.00}", precioNPC);
                    
                    csv.AppendLine($"{idInt},{nombre},{tierInt},{fCosto},{fMercado},{fEtiqueta},{fNPC}");
                    contador++;
                }
                
                File.WriteAllText(rutaArchivo, csv.ToString());
                Logger.LogInfo(string.Format(Traducir("ExitoCSV", "¡Éxito! CSV guardado con {0} productos reales en: {1}"), contador, rutaArchivo));
                
                // ¡ENVIAMOS A GOOGLE SHEETS LEYENDO LA URL DESDE EL ARCHIVO DE CONFIGURACIÓN!
                string urlGoogleScript = configUrlGoogle.Value;
                
                if (!string.IsNullOrEmpty(urlGoogleScript))
                {
                    Logger.LogInfo(Traducir("EnviandoNube", "Enviando datos a la nube de Google Sheets..."));
                    Task.Run(() => 
                    {
                        try 
                        {
                            using (WebClient client = new WebClient()) 
                            {
                                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; // Seguridad HTTPS
                                client.Headers[HttpRequestHeader.ContentType] = "text/plain";
                                client.UploadString(urlGoogleScript, csv.ToString());
                                Logger.LogInfo(Traducir("ExitoNube", "¡Google Sheets actualizado con éxito!"));
                            }
                        } 
                        catch (Exception ex) 
                        {
                            Logger.LogError(Traducir("ErrorGoogle", "Error al conectar con Google Sheets: ") + ex.Message);
                        }
                    });
                }
            }
            else
            {
                Logger.LogError(Traducir("ErrorClase", "Error: No se pudo localizar la clase ProductListing."));
            }
        }

        void BuscarPreciosDeVenta()
        {
            Logger.LogInfo("--- ESCÁNER ABSOLUTO DE LISTAS Y ARRAYS DECIMALES ---");
            Assembly assemblyJuego = Assembly.Load("Assembly-CSharp");
            
            foreach (Type tipo in assemblyJuego.GetTypes())
            {
                FieldInfo[] variables = tipo.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                
                foreach (var v in variables)
                {
                    // Arrays de floats o doubles
                    if (v.FieldType == typeof(float[]) || v.FieldType == typeof(double[]))
                    {
                        Logger.LogInfo($"¡ARRAY ENCONTRADO! Clase: [{tipo.Name}] | Variable: {v.Name} ({v.FieldType.Name})");
                    }
                    // Listas de floats o doubles
                    else if (v.FieldType == typeof(List<float>) || v.FieldType == typeof(List<double>))
                    {
                        Logger.LogInfo($"¡LISTA ENCONTRADA! Clase: [{tipo.Name}] | Variable: {v.Name} ({v.FieldType.Name})");
                    }
                    // Diccionarios que conecten un ID (int) con un Precio (float)
                    else if (v.FieldType.IsGenericType && v.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                    {
                        Type[] args = v.FieldType.GetGenericArguments();
                        if (args[0] == typeof(int) && (args[1] == typeof(float) || args[1] == typeof(double)))
                        {
                            Logger.LogInfo($"¡DICCIONARIO ENCONTRADO! Clase: [{tipo.Name}] | Variable: {v.Name}");
                        }
                    }
                }
            }
            
            Logger.LogInfo("--- FIN DE BÚSQUEDA ---");
        }
    }
}
