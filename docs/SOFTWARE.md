# Catálogo de Softwares e Motores de Instalação

O **UniFAP Lab Manager** conta com um motor unificado de software (`SoftwareEngine`) capaz de orquestrar quatro tipos distintos de instaladores de forma transparente e resiliente.

---

## 🏗️ Modos de Instalação Suportados

| Tipo (`SoftwareType`) | Descrição | Tratamento de Erros / Reboot | Exemplo de Aplicação |
| :--- | :--- | :--- | :--- |
| **`winget`** | Pacotes oficiais via Windows Package Manager CLI. | Aceita automaticamente licenças (`--accept-package-agreements --accept-source-agreements`). Trata código de retorno 3010 (Reboot Required). | Google Chrome, VS Code, Git, Node.js |
| **`script`** | Automação baseada em arquivos `.bat`, `.cmd` ou `.ps1`. | Execução silenciosa desacoplada com captura de logs em tempo real. | Suíte Microsoft Office 2024 / 365 (`365.bat`) |
| **`local`** | Instaladores binários `.exe` ou `.msi` armazenados na pasta institucional `software/`. | Execução com argumentos silenciosos customizados (`/qn`, `/quiet`, `/verysilent`). | Autodesk AutoCAD, AltoQi Eberick, WinRAR |
| **`legacy`** | Softwares acadêmicos antigos com comportamento não determinístico de exit code. | Classificado com gravidade `Warning`. Não aborta o fluxo de preparação caso o exit code seja diferente de zero. | Sniffy the Virtual Rat Pro |

---

## 📦 Estrutura de Declaração de um Software em `config/software.json`

```json
{
  "id": "vscode",
  "name": "Visual Studio Code",
  "category": "Desenvolvimento",
  "description": "Editor de código-fonte moderno com suporte a extensões e depuração.",
  "type": "winget",
  "wingetId": "Microsoft.VisualStudioCode",
  "silentArgs": "--silent --accept-package-agreements --accept-source-agreements",
  "severity": "Warning",
  "estimatedTimeSeconds": 60,
  "requiresReboot": false,
  "legacy": false
}
```

---

## 🏢 Integração com o Office 365 / 2024 da UniFAP

A instituição disponibiliza a ferramenta oficial de implantação do Office (ODT) na pasta `software/Office365/`. O UniFAP Lab Manager executa o arquivo `365.bat` ou dispara diretamente:

```cmd
setup.exe /configure configuration.xml
```

### Arquivo `configuration.xml` Institucional:
```xml
<Configuration>
  <Add OfficeClientEdition="64" Channel="PerpetualVL2024">
    <Product ID="ProPlus2024Volume">
      <Language ID="pt-br" />
      <ExcludeApp ID="Lync" />
      <ExcludeApp ID="OneDrive" />
    </Product>
  </Add>
  <Display Level="None" AcceptEULA="TRUE" />
  <Property Name="AUTOACTIVATE" Value="1" />
</Configuration>
```

---

## 🧪 Tratamento Especial: Sniffy the Virtual Rat Pro

Softwares de psicologia comportamental legados frequentemente utilizam instaladores de 16/32 bits que não retornam códigos de saída padrão do Windows Installer (MSI). 

No UniFAP Lab Manager:
- O software possui a flag `"legacy": true` e gravidade `"severity": "Warning"`.
- Caso a execução retorne código não zero, o status registrado é `SoftwareInstallStatus.Warning` ao invés de `Failed`.
- O relatório final marca o status como **APROVADO COM ADVERTÊNCIAS**, garantindo que as demais instalações continuem normalmente.
