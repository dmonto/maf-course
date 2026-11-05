# Possible exercise solutions

These projects are instructor solutions. They are separate from the seven example projects and do not modify the GitBook statements.

| ID | Topic exercise | Project |
| --- | --- | --- |
| ex01 | Preserve a confirmed fact beyond the conversation window | ex01-confirmed-operating-system/ConfirmedOperatingSystem.csproj |
| ex02 | Make the memory lifetime explicit and test expiry | ex02-configurable-idle-expiry/ConfigurableIdleExpiry.csproj |

Build the root solution first. Both projects load the same root `.env` and reuse the stable repository, MAF configuration and memory policy. Their added domain fields, confirmation behavior and lifetime validation live in this directory.
