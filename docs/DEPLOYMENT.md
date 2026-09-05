# Guia de Implantação e Deployment Automatizado — UniFAP Lab Manager

Este documento especifica os fluxos de implantação do **UniFAP Lab Manager v1.0.0** em ambientes corporativos e acadêmicos, abrangendo desde a execução assistida por pendrive até o provisionamento automatizado via `$OEM$` e `autounattend.xml`.

---

## 🎯 Modelos de Implantação

O sistema foi desenhado para operar com igual eficiência em dois cenários:

```text
┌─────────────────────────────────────────────────────────────┐
│ 1. MÁQUINA JÁ INSTALADA COM WINDOWS                         │
│    Técnico pluga pendrive -> Executa Run.ps1 / App.exe      │
│    Escolhe perfil -> Confirma -> Máquina preparada          │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ 2. INSTALAÇÃO AUTOMATIZADA DO WINDOWS (UNATTEND)            │
│    ISO Windows 11 com autounattend.xml + pasta $OEM$        │
│    Setup limpa disco, instala Windows e aciona Lab Manager   │
└─────────────────────────────────────────────────────────────┘
```

---

## 📦 1. Preparação do Pacote de Distribuição (Pendrive de TI)

Para gerar o diretório de implantação autônomo:

1. Execute o script de publicação na máquina de desenvolvimento:
   ```powershell
   .\Publish.ps1 -Configuration Release
   ```
2. O conteúdo final será gerado em `dist/UniFAP-LabManager/` contendo:
   - `UniFAP.LabManager.App.exe`
   - `Agent/UniFAP.LabManager.Agent.exe`
   - Pastas `config/`, `assets/`, `scripts/`, `software/`, `themes/`
3. Copie a pasta inteira para a raiz do seu pendrive de suporte técnico:
   ```text
   E:\UniFAP-LabManager\
   ```

---

## ⚡ 2. Integração com Instalação Automatizada (`autounattend.xml`)

O projeto inclui um modelo de arquivo de resposta validado em `scripts/autounattend-sample.xml`, inspirado no gerador de referência [Schneegans Unattend Generator](https://schneegans.de/windows/unattend-generator/).

### Estrutura da mídia de instalação do Windows:
```text
ISO_PENDRIVE/
├── autounattend.xml                   # Arquivo de resposta na raiz do pendrive
├── sources/
│   ├── install.wim
│   └── $OEM$/
│       └── $1/                        # Mapeia para C:\
│           └── OEM/
│               └── UniFAP-LabManager/ # Conteúdo gerado pelo Publish.ps1
```

### O que o `autounattend.xml` executa:
1. Pula telas de EULA, conta Microsoft (OOBE) e configuração de rede sem fio.
2. Cria a conta técnica local `suporte` no grupo Administradores.
3. No primeiro logon do usuário (`FirstLogonCommands`), executa o bootstrap do Lab Manager:
   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "if (Test-Path 'C:\OEM\UniFAP-LabManager\UniFAP.LabManager.App.exe') { Start-Process 'C:\OEM\UniFAP-LabManager\UniFAP.LabManager.App.exe' }"
   ```

---

## 🔄 3. Ciclo de Reinicialização e Retomada Autônoma (`Agent`)

Quando uma etapa do Job exige reinicialização do computador (por exemplo, após o ingresso no Active Directory institucional ou instalação de runtimes de sistema):

1. O **Lab Manager** salva o estado completo do Job em:
   ```text
   C:\ProgramData\UniFAP\LabManager\active_job_state.json
   ```
2. Depois das etapas que consomem credenciais, o sistema registra a tarefa UniFAP_LabManager_Resume no Agendador de Tarefas, com execução elevada no próximo login do mesmo técnico. Nenhuma senha é armazenada na tarefa.
3. O computador reinicia. A aplicação aborta o reboot se não conseguir preparar a retomada automática solicitada.
4. No próximo login do técnico, o agente retoma a validação final e a geração do relatório, sem repetir as etapas concluídas. É necessário manter a pasta do pacote disponível.
5. Ao concluir, o estado ativo e a tarefa agendada são removidos. Histórico e relatórios permanecem disponíveis.
6. Com reinicialização automática desativada, reinicie manualmente e abra o aplicativo para concluir a validação. Com retomada automática desativada, abra o aplicativo após o reboot.

O relatório final verifica contas locais, privilégios, nome e domínio. A instalação de software é conferida ao término de cada instalador. Simulações não aplicam configurações e seus relatórios são identificados como simulação.


---

## 🛡️ 4. Estrutura de Diretórios Persistentes no Sistema Operacional

O UniFAP Lab Manager utiliza o padrão corporativo `C:\ProgramData\UniFAP\LabManager\`:

| Diretório | Finalidade |
| :--- | :--- |
| `C:\ProgramData\UniFAP\LabManager\Jobs\` | Histórico individual de todos os Jobs executados na estação (`.json`). |
| `C:\ProgramData\UniFAP\LabManager\Logs\` | Logs rotativos e sanitizados de auditoria técnica (`.log`). |
| `C:\ProgramData\UniFAP\LabManager\Reports\` | Relatórios gerenciais de conformidade e aceite (`.json` e `.txt`). |
| `C:\ProgramData\UniFAP\LabManager\active_job_state.json` | Estado temporário volátil de retomada pós-reboot. |

> [!CAUTION]
> Nenhuma senha, token ou credencial administrativa é gravada nesses arquivos em disco. Todos os dados sensíveis são expurgados da serialização por atributos `[JsonIgnore]`.
