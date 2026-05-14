﻿using System;
using System.IO;
using System.Text;
using BepInEx;
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

        // Clase auxiliar para decirle a BepInEx que dibuje un botón UI en lugar de una caja de texto
        public class BotonExportarUI
        {
            public Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer = DibujarBoton;
        }

        static void DibujarBoton(BepInEx.Configuration.ConfigEntryBase entry)
        {
            // Dibuja el botón usando el sistema gráfico nativo de Unity (IMGUI)
            if (GUILayout.Button("Exportar a Google Sheets", GUILayout.ExpandWidth(true)))
            {
                if (Instancia != null) Instancia.ExportarCSVReal();
            }
        }

        void Awake()
        {
            Instancia = this;
            Logger.LogInfo("¡El mod Exportador de Precios se ha cargado correctamente!");
            
            // Configuramos BepInEx para que lea la URL desde un archivo externo
            configUrlGoogle = Config.Bind("Nube", 
                                          "UrlGoogleSheets", 
                                          "", // El valor por defecto estará vacío
                                          "Pega aquí tu URL de Google Apps Script para sincronizar con la nube.");

            // Agregamos el botón a la ventana de configuración
            Config.Bind("Nube", 
                        "Forzar Sincronización", 
                        "", 
                        new ConfigDescription("Haz clic en el botón para enviar los datos manualmente en cualquier momento.", null, new BotonExportarUI()));
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
                Logger.LogInfo("Tienda iniciada. Sincronización automática de bienvenida...");
                ExportarCSVReal();
            }
            // Si la huella digital cambió (llegó el Jueves y cambió la inflación, O tú cambiaste una etiqueta)
            else if (Math.Abs(hashEstadoAnterior - estadoActual) > 0.001)
            {
                hashEstadoAnterior = estadoActual;
                Logger.LogInfo("¡CAMBIO DE MERCADO (JUEVES) O DE ETIQUETA DETECTADO! Sincronizando con Google Sheets...");
                ExportarCSVReal();
            }
        }

        public void ExportarCSVReal() // <-- ¡Importante hacerlo public para que el botón pueda acceder!
        {
            Logger.LogInfo("--- EXPORTANDO DATOS FINALES (MERCADO, ETIQUETAS Y UMBRALES) ---");
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
                    Logger.LogWarning("No se encontró el catálogo en escena. ¿Estás dentro de la tienda?");
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
                Logger.LogInfo($"¡Éxito! CSV guardado con {contador} productos reales en: {rutaArchivo}");
                
                // ¡ENVIAMOS A GOOGLE SHEETS LEYENDO LA URL DESDE EL ARCHIVO DE CONFIGURACIÓN!
                string urlGoogleScript = configUrlGoogle.Value;
                
                if (!string.IsNullOrEmpty(urlGoogleScript))
                {
                    Logger.LogInfo("Enviando datos a la nube de Google Sheets...");
                    Task.Run(() => 
                    {
                        try 
                        {
                            using (WebClient client = new WebClient()) 
                            {
                                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; // Seguridad HTTPS
                                client.Headers[HttpRequestHeader.ContentType] = "text/plain";
                                client.UploadString(urlGoogleScript, csv.ToString());
                                Logger.LogInfo("¡Google Sheets actualizado con éxito!");
                            }
                        } 
                        catch (Exception ex) 
                        {
                            Logger.LogError("Error al conectar con Google Sheets: " + ex.Message);
                        }
                    });
                }
            }
            else
            {
                Logger.LogError("Error: No se pudo localizar la clase ProductListing.");
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
