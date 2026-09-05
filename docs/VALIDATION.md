# Correções e homologação

## Correções implementadas

- Senhas de suporte e domínio são enviadas pela entrada padrão do PowerShell; argumentos e logs não incluem esses scripts. Comandos codificados são ocultados também na sanitização central.
- Não existe senha padrão de suporte; o provisionamento exige uma senha fornecida pelo técnico e retorna falha diante de erro. Grupos locais são identificados por SID, independentemente do idioma do Windows.
- Nomes de computador são validados antes da execução. Caminhos com prefixos semelhantes fora da base são recusados.
- A simulação não cria atalhos nem modifica contas. Relatórios distinguem simulação de preparação aplicada.
- Reinicialização ocorre antes da validação final, após consumir credenciais. O agente executa com elevação no login do mesmo técnico; é necessário manter o pacote no local de instalação.
- Estado é gravado por substituição atômica. Tarefas terminadas deixam apenas histórico. Aplicativo e agente compartilham bloqueio de execução.
- A validação confere usuários, privilégios, nome e domínio; softwares são conferidos após a instalação. Scripts sem detecção de instalação produzem advertência, não confirmação falsa.
- Relatórios são gerados depois do resultado final. Falha de gravação é exposta como falha do job.
- Catálogo inicial usa snapshot local. Sincronização online é explícita, admite contingência local e os testes usam HTTP simulado.
- Publicação interrompe em falhas do dotnet e evita duplicar diretórios de configuração. A verificação de hash do WinGet não é desativada.

## Verificações automatizadas

Execute dotnet test UniFAP.LabManager.sln -c Release e dotnet build UniFAP.LabManager.sln -c Release.
Os testes de PowerShell verificam sintaxe no Windows PowerShell 5.1. O teste de rollback substitui os comandos de Registro e arquivos por funções simuladas, sem alterar o Windows do desenvolvedor. Relatórios de testes usam diretórios temporários; testes de catálogo não dependem da internet.

## Homologação em estação descartável

1. Executar simulação de perfil administrativo e laboratório; conferir ausência de mudanças em usuários, atalhos e domínio.
2. Preparar laboratório com conta de suporte nova, conta de aluno existente e uma instalação Winget e outra local. Conferir os grupos locais e o relatório.
3. Preparar estação administrativa com nome novo e credenciais autorizadas do AD. Confirmar uma reinicialização e retomada no login do mesmo técnico, com relatório final correto.
4. Repetir com reboot automático e retomada automática desativados. Abrir o aplicativo após reiniciar para concluir a validação.
5. Simular senha rejeitada pela política, DNS indisponível, instalador ausente, cancelamento e diretório de relatórios sem permissão. Confirmar que as falhas aparecem no resultado.
6. Manter snapshot da máquina para restaurar entre cenários. Não executar estes cenários na estação de trabalho do desenvolvedor.

A aprovação nos testes automatizados não comprova ingresso em um domínio real, políticas locais, licenças de terceiros ou compatibilidade de todos os instaladores. Estes cenários precisam da rede, credenciais e instaladores institucionais.

Referência para renomeação antes do ingresso: [Microsoft Add-Computer, opção JoinWithNewName](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/add-computer?view=powershell-5.1).
