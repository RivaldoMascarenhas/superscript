---
name: unifap-labmanager
description: Diretrizes de engenharia, arquitetura, boas práticas e padrões para o desenvolvimento, manutenção e extensão do UNIFAP Lab Manager (.NET 8 WPF MVVM, PowerShell corporativo e automação de estações Windows 11).
---

# UniFAP Lab Manager — Engineering & Skill Guide

Este documento define as regras operacionais, arquiteturais e de desenvolvimento para o projeto **UNIFAP LAB MANAGER**. Qualquer alteração no código-fonte, scripts ou configurações deve aderir rigorosamente a estes padrões.

---

## 🏛️ 1. Arquitetura em Camadas

O sistema é construído sobre o ecossistema **.NET 8.0 LTS**, seguindo Clean Architecture e MVVM estrito:

1. **`UniFAP.LabManager.Core`**:
   - Modelos de dados (DTOs), Enums e Contratos de interface (`Interfaces.cs`).
   - NUNCA deve referenciar WPF, Windows Forms ou bibliotecas de baixo nível de infraestrutura.
   - Propriedades com credenciais em memória SEMPRE devem possuir a anotação `[System.Text.Json.Serialization.JsonIgnore]`.

2. **`UniFAP.LabManager.Infrastructure`**:
   - Executores de baixo nível (`PowerShellRunner`, `ProcessRunner`, `WingetRunner`, `LocalInstallerService`).
   - Adapters de sistema (`WmiAdapter`, `RegistryAdapter`).
   - Persistência e Logs (`MaskedLogManager`, `JobPersistenceStore`, `SecurityService`).

3. **`UniFAP.LabManager.Services`**:
   - Orquestração de negócio (`JobOrchestrator`, `PreCheckService`, `SoftwareEngine`, `PerformanceService`, `SupportToolsService`, `ActiveDirectoryService`, `WindowsConfigurationService`).
   - Registro central de DI em [`ServiceCollectionExtensions.cs`](file:///src/UniFAP.LabManager.Services/ServiceCollectionExtensions.cs).

4. **`UniFAP.LabManager.App`**:
   - Interface desktop WPF com MVVM puro.
   - Navegação por `DataTemplate` na `MainWindow`.
   - Nenhuma lógica de negócio ou chamada direta de processo deve residir no code-behind (`.xaml.cs`).

5. **`UniFAP.LabManager.Agent`**:
   - Processo autônomo acionado via `HKLM\...\RunOnce` pós-reinicialização do Windows.
   - Recupera o Job em disco e dá continuidade às etapas pendentes.

---

## 🔒 2. Princípios de Segurança e Isolamento

### Regra 25 — Isolamento Administrativo vs Acadêmico
- Computadores no modo **Administrativo** NUNCA devem receber softwares específicos de laboratório acadêmico (IDEs pesadas, AutoCAD, Revit, Wireshark, compiladores, emuladores).
- Essa restrição é garantida no código por `BlockedAdministrativeSoftwareIds` no `JobOrchestrator.cs`.

### Zero Hardcoding de Credenciais
- Nenhuma senha de administrador (`suporte`) ou credencial de domínio (Active Directory) pode ser salva em arquivos de configuração JSON, scripts PowerShell em disco ou logs.
- Em trânsito, senhas devem ser tratadas via `SecureString` ou descartadas de memória após uso.
- Todos os logs gerados devem passar pelas expressões regulares do `SecurityService` para sanitização ativa (`[REDACTED]`).

### Proteção de Path Traversal
- Toda manipulação de arquivo, executável local ou script deve validar se o caminho resultante reside dentro do diretório base permitido via `SecurityService.ValidatePathSafety()`.

---

## 📜 3. Padrões Obrigatórios para Scripts PowerShell

Para manter 100% de compatibilidade tanto com **Windows PowerShell 5.1** (nativo do Windows 10/11) quanto com **PowerShell Core 7+**:

1. **Apenas Caracteres ASCII no Código do Script**:
   - Não use caracteres especiais UTF-8 (como `✓`, `✗`, `•`, `💡`) diretamente dentro de strings em arquivos `.ps1` sem codificação compatível. Use marcadores como `[OK]`, `[FALHA]`, `[AVISO]`. O C# na camada de apresentação é responsável por formatar com ícones gráficos.
2. **Não Utilize Operadores Ternários (`? :`)**:
   - O operador ternário `($cond ? 'A' : 'B')` só existe no PowerShell 7+. No PowerShell 5.1 causa erro de sintaxe imediato. Use `if ($cond) { 'A' } else { 'B' }`.
3. **Execução em Memória via `PowerShellRunner`**:
   - Sempre execute comandos via `-EncodedCommand` (Unicode Base64) com `-NoProfile -NonInteractive -ExecutionPolicy Bypass` e `$ProgressPreference = 'SilentlyContinue'`.
4. **Retorno Estruturado em JSON**:
   - Scripts devem expor a função `Write-JsonResult -Success $bool -Message $str -Details @{}` para que a saída possa ser deserializada diretamente em classes C#.

---

## 🎨 4. Padrões de UI & UX em WPF

1. **Paleta de Cores e Estilos**:
   - Utilize sempre os recursos de [`Styles.xaml`](file:///src/UniFAP.LabManager.App/Themes/Styles.xaml): `AppBgBrush`, `SurfaceBrush`, `PrimaryBrush` (`#2563EB`), `SuccessBrush` (`#10B981`), `WarningBrush` (`#F59E0B`), `DangerBrush` (`#EF4444`).
2. **Conversão de Visibilidade**:
   - O `BooleanToVisibilityConverter` suporta valores booleanos, strings (oculta se vazia ou nula) e objetos (oculta se nulo). Use com `Invert="True"` para estados vazios (*empty states*).
3. **Comandos Assíncronos**:
   - Use `AsyncRelayCommand` para qualquer operação demorada. Ele desativa automaticamente o botão durante a execução através do `CommandManager.InvalidateRequerySuggested()`.
4. **Console e Logs**:
   - Toda caixa de texto de log (`ActionLog`, `LiveLogOutput`) deve possuir auto-scroll configurado no code-behind disparado por evento `OnLogAppended`.
