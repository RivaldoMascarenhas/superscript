# UNIFAP LAB MANAGER

> Plataforma desktop corporativa de padronização, provisionamento, configuração, diagnóstico e manutenção automatizada de estações de trabalho Windows 11 para o setor de TI do **Centro Universitário Paraíso - UNIFAP**.

---

## 🏛️ Visão Geral

O **UNIFAP LAB MANAGER** foi desenvolvido para transformar a rotina do suporte técnico e da infraestrutura de TI da UniFAP. Inspirado na ergonomia e maturidade de ferramentas declarativas modernas (como o WinUtil), porém projetado com arquitetura modular própria em C# / .NET 8.0 WPF MVVM, o sistema permite que técnicos preparem computadores administrativos ou acadêmicos com o mínimo de intervenção humana.

### Princípio de Operação do Técnico:

```text
TÉCNICO
   ↓
ABRE O UNIFAP LAB MANAGER
   ↓
ESCOLHE O TIPO DO COMPUTADOR (Administrativo ou Laboratório)
   ↓
ESCOLHE O PERFIL DO CURSO (Geral, ADS, Engenharia, Arquitetura, Psicologia ou Personalizado)
   ↓
CONFIRMA (e insere credencial AD em memória se administrativo)
   ↓
O SISTEMA EXECUTA TUDO
   ↓
REBOOT SE NECESSÁRIO
   ↓
RETOMA AUTOMATICAMENTE (Via UniFAP Agent)
   ↓
VALIDA E GERA RELATÓRIO
```

---

## 🚀 Principais Recursos

1. **Dois Modos Principais**:
   - **ADMINISTRATIVO**: Aplica identidade UniFAP, otimizações seguras, provisionamento de usuários locais (`suporte` admin / `aluno` padrão), instalação de catálogo básico e ingresso no Active Directory institucional com proteção estrita de credenciais.
   - **LABORATÓRIO**: Configura estações para cursos acadêmicos específicos sem ingresso no AD (por padrão), instalando pilhas completas de IDEs, compiladores, CAD, modelagem e softwares científicos.

2. **Perfis de Laboratório Declarativos em JSON**:
   - **Geral**: Chrome, Firefox, Office 2024 (Microsoft 365), WinRAR, VLC.
   - **ADS (Análise e Desenvolvimento de Sistemas)**: Android Studio, Arduino IDE, Docker Desktop, PyCharm Community, IntelliJ IDEA Community, Python 3.11, Node.js LTS, PostgreSQL, pgAdmin 4, VS Code, Wireshark, Dev-C++, Eclipse Temurin JDK 21.
   - **Engenharia**: AutoCAD 2025, Revit 2025, AltoQi Eberick, LINGO, softwares básicos.
   - **Arquitetura**: AutoCAD 2025, Revit 2025, Figma Desktop, softwares básicos.
   - **Psicologia**: R for Windows, Sniffy the Virtual Rat Pro (legado), softwares básicos.
   - **Personalizado**: Interface com busca em tempo real, filtros de categorias, multi-seleção por checkboxes e contador dinâmico.

3. **Motor Multi-Instalador (`SoftwareEngine`)**:
   - Integração com **Winget**, instaladores locais (.exe / .msi), scripts institucionais (.cmd / .bat / .ps1) e softwares legados com tolerância a falhas.
   - Compatibilidade total com o script oficial de instalação do **Office 365 / 2024** da UniFAP.

4. **Retomada Autônoma Pós-Reboot (`UniFAP.LabManager.Agent`)**:
   - Persistência contínua do Job em `C:\ProgramData\UniFAP\LabManager\`.
   - Se uma reinicialização for disparada (pós-ingresso no AD ou instalação de runtime), o agente retoma a validação final no próximo login do técnico, por tarefa agendada elevada, sem repetir as etapas concluídas.

5. **Segurança e Zero Hardcoding**:
   - Nenhuma credencial ou senha fica gravada em código, JSON, script ou arquivo de log.
   - Sanitização ativa de logs (`[REDACTED]`).
   - Bloqueio de ataques de Path Traversal (`..\..`).

6. **Diagnóstico e Manutenção**:
   - Bateria completa de diagnósticos (Hardware, SO, Rede, Active Directory, Segurança, Serviços).
   - Ferramentas independentes: aplicar wallpaper, aplicar performance, rollback de configurações, reparo do Windows (DISM / SFC) e validação de domínio.
   - Emissão de relatórios executivos em JSON e TXT formatado com aprovação institucional.

---

## 📂 Estrutura do Projeto

```text
UniFAP.LabManager/
│
├── src/
│   ├── UniFAP.LabManager.Core/            # Entidades, Enums, Contratos e Modelos DTO
│   ├── UniFAP.LabManager.Infrastructure/  # Executores (PowerShell, Process, Winget, Registry, WMI, Logs)
│   ├── UniFAP.LabManager.Services/        # Orquestrador, PreCheck, AD, Software, Performance, Branding, Relatórios
│   ├── UniFAP.LabManager.App/             # Interface Desktop WPF (MVVM, Temas, Views)
│   ├── UniFAP.LabManager.Agent/           # Agente autônomo de retomada pós-reinicialização
│   └── UniFAP.LabManager.Tests/           # Suite de testes unitários xUnit com Moq
│
├── config/                                # Configurações declarativas JSON
├── themes/                                # Temas Dark e Light
├── assets/                                # Identidade visual e wallpapers institucionais
├── scripts/                               # Scripts PowerShell validados e modulares
├── software/                              # Diretório base para instaladores locais oficiais
├── docs/                                  # Documentação técnica e operacional completa
└── build/                                 # Scripts automatizados de build e instalação
```

---

## 🛠️ Como Compilar e Executar

### Pré-requisitos:
- Windows 10 (1809+) ou Windows 11 (recomendado 24H2)
- .NET 8.0 SDK (LTS) ou posterior
- Privilégios de Administrador local (para execução completa com AD/Users/Performance)

### Scripts Raiz de Automação:

| Script | Finalidade |
| :--- | :--- |
| `.\Build.ps1` | Restaura dependências e compila a solução completa em Release (`dotnet build`). |
| `.\Run.ps1` | Valida privilégios administrativos e inicializa a aplicação desktop WPF (`dotnet run`). |
| `.\Test.ps1` | Executa a suíte de testes xUnit com saída normal/detalhada (`dotnet test`). |
| `.\Publish.ps1` | Empacota os binários, assets e gera o arquivo `UniFAP-LabManager.zip` para distribuição web em `dist/`. |
| `.\lab.ps1` | Bootstrapper web para inicialização remota sem necessidade de clonar o repositório (`irm ... \| iex`). |

### ⚡ Execução Remota via Linha de Comando Única (Estilo WinUtil):

Em qualquer computador da instituição (sem precisar baixar ou configurar nada antes), abra o PowerShell como Administrador e execute o comando oficial ultra curto:

```powershell
irm tinyurl.com/labfap | iex
```

> **Alternativas memoráveis:**
> - `irm tinyurl.com/unifap-lab | iex`
> - `irm https://raw.githubusercontent.com/RivaldoMascarenhas/superscript/main/lab.ps1 | iex`

*O bootstrapper detecta permissões, instala o .NET 8 Desktop Runtime automaticamente caso necessário, baixa o pacote institucional e abre o UNIFAP Lab Manager imediatamente.*

---

## 📄 Documentação Técnica e Operacional Completa

- [Arquitetura Geral do Sistema](file:///docs/ARCHITECTURE.md)
- [Catálogo de Softwares e Sincronização WinUtil](file:///docs/SOFTWARE-CATALOG.md)
- [Guia de Configuração Declarativa JSON](file:///docs/CONFIGURATION.md)
- [Guia de Implantação e Deployment Automatizado ($OEM$ / Unattend)](file:///docs/DEPLOYMENT.md)
- [Active Directory, Credenciais e Políticas de Domínio](file:///docs/ACTIVE-DIRECTORY.md)
- [Resolução de Problemas (Troubleshooting)](file:///docs/TROUBLESHOOTING.md)
- [Guia de Desenvolvimento e Extensão](file:///docs/DEVELOPMENT.md)
- [Políticas de Segurança e Isolamento](file:///docs/SECURITY.md)

---
*Centro Universitário Paraíso - UNIFAP • Setor de Tecnologia da Informação (TI)*

## Validação desta revisão

Consulte [Correções e roteiro de homologação](docs/VALIDATION.md) para conhecer as verificações automatizadas e os cenários que exigem uma estação de laboratório.
