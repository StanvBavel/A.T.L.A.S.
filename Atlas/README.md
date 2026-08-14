# A.T.L.A.S. (Autonomous Technological Logic & Assistance System)

## Projectbeschrijving
A.T.L.A.S. is een moderne, lokaal draaiende AI-assistent geïnspireerd door J.A.R.V.I.S. Het is geen simpele chatbot, maar een robuuste integratie van een Large Language Model (LLM), systeembeheer, en een Personal Knowledge Base (langetermijngeheugen). Het project is ontworpen als een veilige, modulaire desktop/web-assistent die je computer lokaal kan bedienen en tegelijkertijd contextueel bewust is.

De interface is een futuristische HUD (Head-Up Display) gebouwd in Vanilla HTML/CSS/JS, aangedreven door een high-performance .NET 8 ASP.NET Core backend. Communicatie verloopt realtime via SignalR.

## Prerequisites
Voor het succesvol draaien van A.T.L.A.S. op een Windows-machine heb je het volgende nodig:
- **Besturingssysteem:** Windows 10 of 11 (voor volledige System Control functies).
- **.NET SDK:** [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- **Ollama:** [Ollama](https://ollama.com/) geïnstalleerd en draaiend op `http://localhost:11434`.
- **LLM Model:** Een model gedownload in Ollama (bijv. `llama3.2` of `phi3`). Voer `ollama run llama3.2` uit in je terminal.

## Installatie & Configuratie
A.T.L.A.S. gebruikt de Options Pattern. We vermijden hardcoded instellingen.

1. Clone de repository naar je lokale machine.
2. Maak een `appsettings.json` (indien nog niet aanwezig) in de `Atlas/Atlas.Api/` map met de volgende inhoud:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=atlas.sqlite"
  },
  "AiSettings": {
    "OllamaEndpoint": "http://localhost:11434/api/generate",
    "ModelName": "llama3.2"
  }
}
```

## Starten
Om A.T.L.A.S. te starten:
1. Zorg dat Ollama op de achtergrond draait.
2. Open een terminal/opdrachtprompt in de root van het project (`Atlas/Atlas.Api`).
3. Voer het volgende commando uit:
   ```bash
   dotnet run
   ```
4. De applicatie zal opstarten en de SQLite database automatisch genereren.
5. Open je browser en ga naar `http://localhost:5258` (of de poort getoond in de console) om de A.T.L.A.S. HUD te laden.

## Architectuur
A.T.L.A.S. volgt de Clean Architecture principes.
- **Frontend (`wwwroot`):** Bestaat uitsluitend uit Vanilla JS, HTML en CSS. Geen complexe frameworks. Het verzorgt de grafische HUD, audio-opname via de Web Speech API (Wake-word detectie) en Spraaksynthese (TTS).
- **SignalR Hub (`AtlasHub`):** Vormt de brug tussen frontend en backend. Alle commando's en UI-statussen (zoals THINKING, PROCESSING) verlopen asynchroon in realtime.
- **Backend (C#/.NET 8):** Beheert de veiligheid via de `PermissionEngine`. Tools (`IAtlasTool`) en Plugins (`IAtlasPlugin`) worden uitgevoerd via de `ToolDispatcher`. Gevaarlijke commando's sturen een `RequireUserConsent` event naar de frontend voordat ze de executie vervolgen.
- **LLM Integratie:** Geabstraheerd achter `IAiProvider`, waardoor de applicatie model-agnostisch is, hoewel standaard geconfigureerd voor lokaal gebruik via Ollama.
