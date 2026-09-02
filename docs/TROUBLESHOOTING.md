# Resolução de Problemas (Troubleshooting)

Este guia documenta diagnósticos rápidos e soluções para os cenários operacionais mais comuns encontrados durante a preparação de computadores no UniFAP Lab Manager.

---

## 1. Falha no Ingresso ao Active Directory

### Sintoma:
Mensagem de erro: `"Não foi possível resolver o endereço do controlador de domínio DC01.UNIFAP.LOCAL"` ou timeout na etapa de Active Directory.

### Causa:
Configuração incorreta do servidor DNS da placa de rede local ou cabo de rede desconectado da VLAN institucional.

### Solução:
1. Abra a aba **Diagnóstico** ou **Ferramentas** no aplicativo e execute `Validar Active Directory`.
2. Verifique se o DNS primário da placa de rede aponta para o IP do controlador de domínio (`DC01.UNIFAP.LOCAL`).
3. No PowerShell, teste a conectividade com:
   ```powershell
   Resolve-DnsName "UNIFAP.LOCAL"
   Test-NetConnection -ComputerName "DC01.UNIFAP.LOCAL" -Port 389
   ```

---

## 2. Winget Retorna Código de Saída 3010

### Sintoma:
O log de instalação de um software indica `ExitCode: 3010`.

### Diagnóstico:
O código 3010 no Windows Installer significa **ERROR_SUCCESS_REBOOT_REQUIRED** (Instalação concluída com sucesso, mas o sistema requer reinicialização para carregar drivers ou variáveis de ambiente).

### Comportamento do UniFAP Lab Manager:
O `WingetRunner` reconhece automaticamente o código 3010 como sucesso com aviso (`Warning`) e continua a fila de preparação, sinalizando que a máquina deve ser reiniciada no término do job.

---

## 3. O Software Legado (Sniffy) não Instala em Modo Silencioso

### Sintoma:
O instalador do Sniffy permanece aberto em segundo plano aguardando interação ou fecha com código diferente de 0.

### Solução:
1. Certifique-se de que o executável oficial está posicionado em `software/Sniffy/setup.exe`.
2. O UniFAP Lab Manager aplica a política de tolerância a falhas para instaladores legados (`legacy: true`), registrando advertência no relatório sem interromper o lote.
3. Se necessário, finalize a configuração manual pontual do software e consulte o relatório em `C:\ProgramData\UniFAP\LabManager\Reports\`.

---

## 4. Reversão de Otimizações de Desempenho (Rollback)

### Sintoma:
Algum aplicativo acadêmico legado apresentou incompatibilidade com as chaves de registro otimizadas.

### Solução:
1. Acesse a aba **Ferramentas** no UniFAP Lab Manager.
2. Clique no botão **"Restaurar"** no card **"Reverter Otimizações (Rollback)"**.
3. O sistema lerá o snapshot salvo antes da preparação em `C:\ProgramData\UniFAP\LabManager\Rollback\performance_rollback_*.json` e restaurará os valores originais do registro do Windows.

---

## 5. Localização dos Logs de Auditoria

Todos os registros de depuração e auditoria em tempo real são gravados em:
```text
C:\ProgramData\UniFAP\LabManager\Logs\unifap_labmanager_YYYYMMDD.log
```
Todos os dados sensíveis são previamente sanitizados antes da gravação em disco.
