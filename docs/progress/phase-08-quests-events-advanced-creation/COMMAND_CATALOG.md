# Phase 8 — COMMAND_CATALOG

Typed event commands for the authoritative server interpreter (P8-2+).

| Command | Discriminator | Min phase | Authority | Transaction | Client-visible |
| --- | --- | --- | --- | --- | --- |
| Show text | `show_text` | P8-2 | session | none | yes |
| Start dialogue | `start_dialogue` | P8-2/3 | session | none | yes |
| Conditional branch | `branch` | P8-2 | session | none | no |
| Set character switch | `set_switch` | P8-2 | character | yes | no |
| Set/add/subtract variable | `set_variable` / `add_variable` / `sub_variable` | P8-2 | character | yes | no |
| Give/take item | `give_item` / `take_item` | P8-2 | character | yes | yes |
| Give/take gold | `give_gold` / `take_gold` | P8-2 | character | yes | yes |
| Start quest | `start_quest` | P8-2/3 | character | yes | yes |
| Advance/turn in quest | `advance_quest` / `turn_in_quest` | P8-2/3 | character | yes | yes |
| Teleport | `teleport` | P8-2 | character | yes | yes |
| Wait | `wait` | P8-2 | session | none | no |
| Call common event | `call_common_event` | P8-2/6 | session | varies | varies |

Each command defines: schema version, typed parameters, validation, cancellation, and idempotency rules. Full schemas added as commands are implemented.

## Phase 9

Not started.
