# Roadmap and Milestones
**Language:** [Spanish](Hoja-de-Ruta-y-Hitos)


This roadmap reflects the six project Milestones agreed for the 22-Issue backlog. The dates are planning targets, not guarantees. Milestones and Issue associations are maintained in GitHub; update this page if their dates or scope change.

## M1 — Foundation and Architecture
**Due date:** 16 October 2026

**Purpose:** Establish a buildable .NET 10 solution and define project responsibilities, dependencies, and integration boundaries.

**Issues:**
- [#1 — Rebuild the .NET 10 solution foundation and project structure](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/1)
- [#16 — Define the Example application architecture and integration boundaries](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/16)

## M2 — KUKULCAN Integrations and Authentication
**Due date:** 30 October 2026

**Purpose:** Integrate the verified public contracts of SharedKernel, Auth, Database, and JsonEngine, and consume the separate I18N Web API with secure configuration and tested error handling.

**Issues:**
- [#2 — Demonstrate KUKULCAN.SharedKernel integration](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/2)
- [#3 — Implement authentication and safe administrator bootstrap](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/3)
- [#4 — Integrate Database with configurable providers](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/4)
- [#5 — Demonstrate JsonEngine integration](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/5)
- [#6 — Integrate the I18N Web API service](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/6)

## M3 — Sample Domain and Database Providers
**Due date:** 13 November 2026

**Purpose:** Define the domain model and schema `Sample`, make migrations repeatable, and validate PostgreSQL, SQL Server, and MySQL independently where supported.

**Issues:**
- [#7 — Define the Sample domain model and database schema](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/7)
- [#17 — Implement database migrations and Sample schema initialization](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/17)
- [#20 — Validate PostgreSQL persistence](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/20)
- [#21 — Validate Microsoft SQL Server persistence](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/21)
- [#22 — Validate MySQL persistence](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/22)

## M4 — Web API and Business Rules
**Due date:** 27 November 2026

**Purpose:** Implement API endpoints and search filters, validation, safe updates, soft deletion for clients/providers, and retention of commercial history.

**Issues:**
- [#8 — Implement Example Web API endpoints and search filters](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/8)
- [#18 — Implement entity updates, validation, and business rules](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/18)
- [#19 — Enforce soft deletion and historical-data retention](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/19)

## M5 — Four Web User Interfaces
**Due date:** 11 December 2026

**Purpose:** Deliver separately buildable Razor Pages, Blazor, Angular, and React clients that all consume the same API, with login as the initial screen and secure session handling.

**Issues:**
- [#9 — Razor web interface](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/9)
- [#10 — Blazor web interface](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/10)
- [#11 — Angular web interface](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/11)
- [#12 — React web interface](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/12)

## M6 — Quality, CI, and Documentation
**Due date:** 18 December 2026

**Purpose:** Complete behavior-focused testing and implementation-aligned documentation while ensuring CI, coverage, provider checks, and client checks exercise real projects.

**Issues:**
- [#13 — Add behavior-focused API, authentication, and persistence tests](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/13)
- [#14 — Adapt and validate CI, reference-client, coverage, and branch-cleanup workflows](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/14)
- [#15 — Align repository documentation with the implemented Example](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/15)

## Schedule realism

These dates provide staged checkpoints across approximately nine and a half weeks from mid-October to mid-December 2026. The schedule is a planning estimate based on the observed elapsed activity windows in the related KUKULCAN repositories, not a measurement of effort-hours. Work in those repositories overlapped, and PR dates do not capture every planning, testing, or offline work session. Reassess dates after the solution exists and the initial API/persistence scope is known.
