# Guia de Desenvolvimento e Extensão

Este documento orienta os desenvolvedores e engenheiros de software da TI da UniFAP sobre como manter, extender e adicionar novos recursos ao **UNIFAP LAB MANAGER**.

---

## 🛠️ Ambiente de Desenvolvimento Recomendado

- **IDE**: Visual Studio 2022 (com workload Desktop .NET) ou VS Code com extensão C# Dev Kit.
- **SDK**: .NET 8.0 SDK (x64).
- **Sistema Operacional**: Windows 11 (recomendado para testar APIs de desktop e temas).

---

## ➕ Como Adicionar um Novo Software ao Catálogo

1. Abra o arquivo `config/software.json`.
2. Adicione uma nova entrada na lista `"items"`. Exemplo:
   ```json
   {
     "id": "blender",
     "name": "Blender 3D",
     "category": "Design e Mídia",
     "description": "Suíte aberta de modelagem 3D, animação e renderização.",
     "type": "winget",
     "wingetId": "BlenderFoundation.Blender",
     "silentArgs": "--silent --accept-package-agreements --accept-source-agreements",
     "severity": "Warning",
     "estimatedTimeSeconds": 120,
     "requiresReboot": false,
     "legacy": false
   }
   ```
3. Se desejar incluí-lo em um perfil padrão (ex: Arquitetura), abra `config/profiles.json` e insira o ID `"blender"` no array `"software"` do perfil desejado.

---

## ➕ Como Criar um Novo Perfil de Laboratório

Para criar um novo perfil (ex: Curso de Medicina ou Direito):
1. Abra `config/profiles.json`.
2. Adicione um novo objeto na lista `"laboratories"`:
   ```json
   {
     "id": "medicina",
     "displayName": "Medicina e Saúde",
     "description": "Atlas anatômicos, softwares de simulação clínica e pesquisa em saúde",
     "software": ["chrome", "firefox", "office365", "winrar", "vlc"]
   }
   ```
3. O UniFAP Lab Manager carregará automaticamente o novo card na interface sem necessidade de recompilação.

---

## 🧪 Executando Testes Unitários

A suíte de testes xUnit valida regras de negócio, serialização JSON, segurança e fluxo de jobs:

```powershell
dotnet test src/UniFAP.LabManager.Tests/UniFAP.LabManager.Tests.csproj
```
