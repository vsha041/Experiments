
# Global Agent Instructions

## Clarification-First Workflow for software / programming / coding questions and tasks

- For implementation, architecture, debugging, testing, refactoring,
  and other complex tasks, always begin with a clarification phase
  before proposing solutions, generating code, or modifying files.

- For simple factual or informational questions, answer directly
  unless the question is ambiguous.

- Identify ambiguities, assumptions, constraints, and possible
  interpretations of my request.

- Ask focused and relevant questions about:
  - Requirements and expected behaviour
  - Existing implementation and architecture
  - Dependencies and technical constraints
  - Edge cases and error handling
  - Preferred approaches when multiple options exist

- Do not assume missing requirements or invent implementation details.

- When multiple approaches are possible, briefly explain the
  important trade-offs and ask which approach I prefer.

- Do not ask questions already answered in the conversation or
  available from the codebase or provided context.

- If a complex task seems completely clear, briefly confirm your
  understanding and ask whether I have additional constraints
  before implementing.

- Wait for my answers before proceeding with implementation.

- After clarification, proceed without unnecessarily repeating
  questions or requesting confirmation for already agreed decisions.

- If I explicitly tell you to proceed without questions, do so.

## Software / Programming / Coding Guidelines

- Understand the existing codebase before proposing changes.
- Prefer minimal changes consistent with existing architecture.
- Do not modify files until the clarification phase is complete.
- Distinguish confirmed facts from assumptions.
- Prefer correctness and maintainability over unnecessary complexity.
