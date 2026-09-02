# Guia Completo do Catálogo de Softwares — UniFAP Lab Manager

Este documento orienta os técnicos e administradores de TI do **Centro Universitário Paraíso - UNIFAP** sobre como gerenciar, adicionar, remover, personalizar e sincronizar softwares e perfis de laboratório **sem alterar nenhuma linha de código da aplicação**.

---

## 🏛️ Filosofia do Catálogo

O UniFAP Lab Manager adota arquitetura **100% orientada a dados declarativos (Data-Driven Architecture)**:
1. **Código Estável**: O binário compilado não possui listas de softwares ou caminhos fixos.
2. **Prioridade Soberana UniFAP**: Caso um software exista tanto no catálogo institucional da UniFAP quanto no catálogo importado do WinUtil, as definições da UniFAP (versão, instalador local, argumentos silenciosos) **sempre prevalecem**.
3. **Offline-First**: Um snapshot local (`config/winutil-applications.json`) permite operação plena mesmo em bancadas sem acesso à internet.

---

## 📂 Estrutura de Arquivos de Catálogo

```text
config/
├── software.json                  # Catálogo institucional prioritário da UniFAP
├── profiles.json                  # Definição dos perfis de laboratório e administrativo
├── catalog-source.json            # Metadados de sincronização externa (WinUtil)
└── winutil-applications.json      # Snapshot local de contingência do WinUtil
```

---

## ➕ 1. Como Adicionar um Novo Software ao Catálogo

Abra o arquivo `config/software.json` e adicione uma nova entrada no array `"items"`.

### Exemplo 1: Software via WinGet (Automático)
```json
{
  "id": "git",
  "name": "Git for Windows",
  "category": "Desenvolvimento",
  "description": "Sistema de controle de versão distribuído",
  "type": "winget",
  "wingetId": "Git.Git",
  "silent": true,
  "severity": "Warning",
  "iconKey": "Code",
  "source": "UniFAP",
  "officialLink": "https://git-scm.com/",
  "isOpenSource": true
}
```

### Exemplo 2: Software Proprietário / Local (EXE)
Coloque o instalador em `software/NomeDoSoftware/` e configure:
```json
{
  "id": "sketchup2025",
  "name": "Trimble SketchUp Pro 2025",
  "category": "Arquitetura",
  "description": "Software de modelagem 3D para projetos arquitetônicos",
  "type": "local",
  "installer": "software/Trimble/SketchUp",
  "entryPoint": "setup.exe",
  "silentArgs": "/silent /norestart",
  "severity": "Warning",
  "iconKey": "Building",
  "source": "UniFAP"
}
```

### Exemplo 3: Software via Pacote MSI
```json
{
  "id": "node_lts_msi",
  "name": "Node.js LTS (MSI)",
  "category": "Desenvolvimento",
  "description": "Ambiente de execução JavaScript no servidor",
  "type": "msi",
  "installer": "software/NodeJS/node-v20-x64.msi",
  "silentArgs": "/qn /norestart",
  "severity": "Warning",
  "iconKey": "Node",
  "source": "UniFAP"
}
```

### Exemplo 4: Software Legado com Tolerância a Falhas (ex: Sniffy Pro)
```json
{
  "id": "sniffy",
  "name": "Sniffy the Virtual Rat Pro",
  "category": "Psicologia",
  "description": "Simulador de condicionamento operante para psicologia experimental",
  "type": "local",
  "legacy": true,
  "installer": "software/Sniffy",
  "entryPoint": "setup.exe",
  "silentArgs": "/S",
  "severity": "Optional",
  "iconKey": "Brain",
  "source": "UniFAP"
}
```
> [!NOTE]
> Quando `legacy: true` ou `severity: "Optional"`, eventuais códigos de saída não-zero do instalador são tratados como **Warning** e **não abortam** a preparação do computador.

---

## ➖ 2. Como Remover um Software

1. Abra `config/software.json` e exclua o objeto do software dentro da lista `"items"`.
2. Caso o software esteja referenciado em algum perfil de `config/profiles.json`, remova seu ID do array `"software"` daquele laboratório.

---

## 🏫 3. Como Criar um Novo Laboratório ou Alterar Perfil

Abra o arquivo `config/profiles.json`:

```json
{
  "laboratories": {
    "meu_novo_laboratorio": {
      "id": "meu_novo_laboratorio",
      "displayName": "Laboratório de Inteligência Artificial",
      "description": "Ambiente dedicado a Machine Learning, Visão Computacional e Data Science.",
      "joinDomain": false,
      "software": [
        "chrome",
        "firefox",
        "office365",
        "winrar",
        "python311",
        "vscode",
        "docker"
      ]
    }
  }
}
```
Ao reabrir a aplicação ou trocar de tela, o novo perfil estará imediatamente visível na seleção de **Laboratório**.

---

## 🔒 4. Blindagem do Perfil Administrativo

O perfil **Administrativo** (`"administrative"`) é protegido por código:
- Somente softwares corporativos homologados (`chrome`, `firefox`, `office365`, `winrar`) podem ser instalados.
- Softwares acadêmicos (AutoCAD, Revit, Eberick, Docker, Sniffy, etc.) são **filtrados e descartados por código** caso adicionados acidentalmente.

---

## 🔄 5. Como Sincronizar o Catálogo WinUtil

### Pela Interface Gráfica (WPF):
1. Acesse o menu lateral **Software**.
2. Clique no botão **🔄 Sincronizar Catálogo WinUtil** no topo superior direito.
3. A aplicação se conecta ao repositório oficial do WinUtil (`https://raw.githubusercontent.com/ChrisTitusTech/winutil/main/config/applications.json`), valida os schemas, normaliza as categorias e mescla os aplicativos.
4. O resultado exibe a contagem de softwares preservados, mesclados e novos.

### Modo Offline:
Caso a máquina esteja sem internet, o serviço carrega instantaneamente o snapshot empacotado `config/winutil-applications.json` sem emitir erros impeditivos.

---

## 🏷️ 6. Categorias Padronizadas

O catálogo interno normaliza categorias externas em 14 grupos estritos:
* `Browsers`
* `Development`
* `Document`
* `Education`
* `Games`
* `Multimedia`
* `Networking`
* `Utilities`
* `Microsoft Tools`
* `Pro Tools`
* `Communication`
* `Productivity`
* `Security`
* `Other`
