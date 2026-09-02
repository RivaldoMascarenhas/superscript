# Active Directory e Políticas de Credenciais

## 🛡️ Diretrizes Institucionais de Segurança

O ingresso de computadores no domínio institucional `UNIFAP.LOCAL` envolve privilégios administrativos no Active Directory. Para manter a conformidade de segurança e auditoria da UniFAP:

1. **PROIBIDO HARDCODING**: Nenhuma senha de administrador de domínio é gravada no código-fonte, nos arquivos JSON ou nos scripts.
2. **ISOLAMENTO EM MEMÓRIA VOLÁTIL**: As credenciais são coletadas exclusivamente através da janela modal protegida (`ActiveDirectoryDialog.xaml`), mantidas em memória volátil e limpas imediatamente após a execução do comando.
3. **MASCARAMENTO AUTOMÁTICO DE LOGS**: Qualquer comando ou linha de log contendo a palavra `password`, `senha` ou `credential` tem seus valores substituídos por `[REDACTED]`.

---

## 🌐 Pré-Validação de Rede (Pre-Check AD)

Antes de solicitar a senha ao técnico, o `ActiveDirectoryService` realiza as seguintes validações:
1. **Resolução de DNS**: Executa `[System.Net.Dns]::GetHostAddresses("UNIFAP.LOCAL")`.
2. **Ping / Conectividade TCP**: Testa conexão com o Domain Controller (`DC01.UNIFAP.LOCAL`) na porta LDAP (389) ou Kerberos (88).
3. **Verificação de Associação Atual**: Se a máquina já estiver no domínio, a etapa é marcada como concluída para evitar duplicidade.

---

## ⚡ Processo de Ingresso (`Add-Computer`)

A adição é realizada invocando o script institucional `scripts/AD-Join.ps1`:

```powershell
Add-Computer -DomainName "UNIFAP.LOCAL" `
             -Credential $cred `
             -OUPath "OU=Computadores,OU=Administrativo,DC=UNIFAP,DC=LOCAL" `
             -Force
```

---

## 🔄 Retomada Automática Pós-Ingresso

O ingresso no Active Directory requer que a máquina seja reiniciada para que as Políticas de Grupo (GPOs) e o token de máquina sejam reconhecidos pelo controlador de domínio.

1. O `JobOrchestrator` salva o estado pendente em `C:\ProgramData\UniFAP\LabManager\active_job_state.json`.
2. A máquina reinicia (`shutdown /r /t 10`).
3. Ao religar, o `UniFAP.LabManager.Agent` detecta o arquivo de estado e retoma as etapas subsequentes (ex: instalação de softwares e relatório final) de forma transparente.
