# Políticas de Segurança e Isolamento

## 🛡️ Princípios de Segurança Institucional

O **UNIFAP LAB MANAGER** foi concebido sob princípios rigorosos de segurança corporativa para garantir conformidade em ambientes de ensino e administração universitária.

---

## 1. Segregação de Privilégios Locais
- **Conta `suporte`**: Membro do grupo local `Administrators`, destinada exclusivamente para intervenções técnicas da equipe de TI.
- **Conta `aluno`**: Membro exclusivo do grupo local `Users` (Standard). Em nenhuma circunstância o sistema concede privilégios de administrador para contas de estudantes.

---

## 2. Proteção contra Path Traversal e Execução Não Autorizada
- Todos os caminhos de arquivos relativos (scripts, executáveis de software, templates XML) são validados pelo `SecurityService.ValidatePathSafety()`.
- Tentativas de escapar do diretório raiz (`../../`, `..\..`) são bloqueadas antes de qualquer chamada ao sistema operacional.

---

## 3. Gestão de Credenciais e Sanitização de Logs
- Nenhuma senha é armazenada de forma persistente.
- A entrada de credenciais do Active Directory ocorre em campos `PasswordBox` em memória volátil.
- Todos os sinks de log e eventos em tempo real passam pelo motor de mascaramento regex, substituindo senhas, chaves de ativação e tokens por `[REDACTED]`.

---

## 4. Auditoria e Rastreabilidade
- Cada execução gera um identificador único de Job (`UNIFAP-YYYYMMDD-HHMMSS`).
- Os relatórios de preparação registram data, hora, usuário executor, máquina de destino, checksum de etapas e veredito final institucional.
