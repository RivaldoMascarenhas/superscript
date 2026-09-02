# Guia de Configuração Declarativa — UNIFAP LAB MANAGER

O comportamento do UniFAP Lab Manager é 100% parametrizado por arquivos declarativos em formato **JSON** localizados na pasta `config/`. Isso permite que a equipe de TI altere perfis, regras de domínio, softwares e usuários sem necessidade de recompilar a aplicação.

---

## 📁 Estrutura de Arquivos de Configuração

```text
config/
├── institution.json       # Dados institucionais, nome oficial e setor
├── active-directory.json  # Parâmetros de domínio e OUs institucionais
├── branding.json          # Papel de parede, tela de bloqueio e OEM
├── users.json             # Contas locais e políticas de privilégio
├── performance.json       # Otimizações de registro do Windows
├── software.json          # Catálogo completo de softwares e instaladores
├── profiles.json          # Associação de perfis (Geral, ADS, Eng, etc.)
└── settings.json          # Preferências da aplicação e persistência
```

---

## 1. `institution.json`
Define os dados que constam no cabeçalho das interfaces e nos relatórios de auditoria:

```json
{
  "name": "Centro Universitário Paraíso - UNIFAP",
  "shortName": "UniFAP",
  "department": "Setor de Tecnologia da Informação (TI)",
  "supportEmail": "ti@unifap.edu.br",
  "portalUrl": "https://unifap.edu.br",
  "version": "1.0.0"
}
```

---

## 2. `active-directory.json`
Configuração para o ingresso de computadores administrativos na rede da instituição:

```json
{
  "domain": "UNIFAP.LOCAL",
  "domainController": "DC01.UNIFAP.LOCAL",
  "computerOu": "OU=Computadores,OU=Administrativo,DC=UNIFAP,DC=LOCAL",
  "academicOu": "OU=Laboratorios,OU=Academico,DC=UNIFAP,DC=LOCAL",
  "joinTimeoutSeconds": 180,
  "restartAfterJoin": true
}
```

---

## 3. `branding.json`
Identidade visual institucional:

```json
{
  "wallpaperPath": "assets/branding/wallpaper/papel_de_parede_unifap.jpg",
  "lockscreenPath": "assets/branding/wallpaper/papel_de_parede_unifap.jpg",
  "oemManufacturer": "Centro Universitário Paraíso - UNIFAP",
  "oemSupportHours": "Segunda a Sexta: 07h às 22h | Sábado: 08h às 12h",
  "oemSupportPhone": "(88) 3512-3211",
  "oemSupportUrl": "https://unifap.edu.br/suporte"
}
```

---

## 4. `users.json`
Regras para criação de contas locais:
- `suporte`: Administrador local para manutenção da TI.
- `aluno`: Usuário comum sem privilégios administrativos.

```json
{
  "localAccounts": [
    {
      "username": "suporte",
      "fullName": "Suporte Técnico UniFAP",
      "isAdmin": true,
      "promptPassword": true,
      "defaultPassword": "",
      "passwordNeverExpires": true
    },
    {
      "username": "aluno",
      "fullName": "Aluno UniFAP",
      "isAdmin": false,
      "promptPassword": false,
      "defaultPassword": "",
      "passwordNeverExpires": true
    }
  ]
}
```

---

## 5. `performance.json`
Otimizações seguras de latência e telemetria:
- **`preserveClearType`**: Garante que o suavizador de fontes sub-pixel permaneça ativado.
- **`preserveAnimations`**: Mantém as animações de janela ativas para manter a estética do Windows 11.
- **`preserveThumbnails`**: Garante geração de miniaturas de imagens e PDFs no Windows Explorer.

```json
{
  "disableTelemetry": true,
  "disableCortana": true,
  "disableBingSearch": true,
  "disableGameDVR": true,
  "setMenuShowDelay": 50,
  "preserveClearType": true,
  "preserveThumbnails": true,
  "preserveAnimations": true,
  "enableStorageSense": true,
  "disableHibernation": true
}
```

---

## 6. `profiles.json`
Mapeamento de perfis para cada modalidade e curso acadêmico:

```json
{
  "administrative": {
    "name": "Administrativo",
    "description": "Estações para coordenações, secretarias e setores administrativos",
    "joinDomain": true,
    "software": ["chrome", "firefox", "office365", "winrar", "adobereader", "vlc", "anydesk"]
  },
  "laboratories": [
    {
      "id": "geral",
      "displayName": "Laboratório Geral / Comum",
      "description": "Navegação, suíte de escritório e utilitários acadêmicos padrão",
      "software": ["chrome", "firefox", "office365", "winrar", "adobereader", "vlc"]
    },
    {
      "id": "ads",
      "displayName": "Análise e Desenvolvimento de Sistemas (ADS)",
      "description": "Ambiente completo de desenvolvimento, bancos de dados, IDEs e compiladores",
      "software": ["chrome", "firefox", "office365", "winrar", "vscode", "python311", "nodejs", "postgresql", "pgadmin4", "docker", "androidstudio", "arduinoide", "pycharm", "intellij", "wireshark", "devcpp", "temurin-jdk21", "git"]
    },
    {
      "id": "engenharia",
      "displayName": "Engenharias (Civil / Produção)",
      "description": "Software de desenho técnico, cálculo estrutural e simulação",
      "software": ["chrome", "firefox", "office365", "winrar", "autocad2025", "revit2025", "eberick", "lingo"]
    },
    {
      "id": "arquitetura",
      "displayName": "Arquitetura e Urbanismo",
      "description": "Modelagem tridimensional, renderização e projeto arquitetônico",
      "software": ["chrome", "firefox", "office365", "winrar", "autocad2025", "revit2025", "figma"]
    },
    {
      "id": "psicologia",
      "displayName": "Psicologia",
      "description": "Ambientes de análise estatística e simulação comportamental",
      "software": ["chrome", "firefox", "office365", "winrar", "r-project", "sniffy"]
    }
  ]
}
```

---

## 7. `catalog-source.json`
Parâmetros para a sincronização de catálogos externos (WinUtil) e taxonomia:

```json
{
  "winutilSourceUrl": "https://raw.githubusercontent.com/ChrisTitusTech/winutil/main/config/applications.json",
  "fallbackLocalFile": "config/winutil-applications.json",
  "lastSyncUtc": null,
  "totalUniFapItems": 25,
  "totalWinUtilItems": 0,
  "mergedItems": 0,
  "categories": [
    "Browsers", "Development", "Document", "Education", "Games",
    "Multimedia", "Networking", "Utilities", "Microsoft Tools",
    "Pro Tools", "Communication", "Productivity", "Security", "Other"
  ]
}
```

---

## 🛠️ Guia Rápido de Customização para o Técnico

### 1. Alterar Papel de Parede da Instituição
1. Substitua o arquivo de imagem em:
   ```text
   assets/branding/wallpaper/papel_de_parede_unifap.jpg
   ```
2. Caso mude o nome ou formato, atualize a propriedade `"path"` em `config/branding.json`.

### 2. Alterar Parâmetros do Active Directory
1. Abra `config/active-directory.json`.
2. Altere `"domain"`, `"domainController"`, `"computerOu"` e os IPs de `"dnsServers"`.
3. Não insira senhas no arquivo — a credencial é solicitada em memória e expurgada após o join.

### 3. Ajustar Otimizações de Desempenho
1. Abra `config/performance.json`.
2. Ligue (`true`) ou desligue (`false`) opções sob `"visualEffects"`, `"systemServices"` e `"storageAndPower"`.
3. Lembre-se: os componentes essenciais de segurança do Windows (Defender, Firewall, Windows Update) permanecem sempre ativos.
