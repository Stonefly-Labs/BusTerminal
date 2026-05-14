# Spec 001 — Brand System and Design Foundation

## Spec Metadata

| Field | Value |
|---|---|
| Spec ID | 001 |
| Spec Name | brand-system-and-design-foundation |
| Product | BusTerminal |
| Status | Draft |
| Priority | Foundational |
| Dependencies | BusTerminal Constitution |
| Primary Stakeholders | Product, UX, Frontend Engineering, Developer Experience |

---

# 1. Purpose

This specification defines the foundational brand system, visual language, design standards, frontend primitives, accessibility expectations, and reusable UI conventions for BusTerminal.

The objective is to establish a cohesive and scalable design foundation before implementation of functional product features.

This spec ensures:

- Consistent product identity across web UI, documentation, CLI, and future SaaS experiences
- Reusable frontend primitives for rapid agentic development
- High-quality UX standards that reduce visual inconsistency
- Accessibility-first implementation patterns
- A modern technical design system compatible with Next.js and shadcn/ui
- A visual identity aligned with enterprise integration tooling while remaining modern and approachable

---

# 2. Vision

BusTerminal should feel:

- Industrial but polished
- Infrastructure-focused without looking dated
- Fast, technical, and operationally trustworthy
- Modern cloud-native rather than legacy enterprise
- Friendly to developers and architects
- Visually clean even when displaying large amounts of messaging metadata
- Equally usable in light and dark themes

The visual identity should communicate:

- Reliability
- Observability
- Connectivity
- Message flow
- Distributed systems
- Operational confidence
- Modern cloud engineering

---

# 3. Product Branding

## 3.1 Working Product Name

Primary product name:

# BusTerminal

Formatting rules:

- Use `BusTerminal` in all branding and UI
- Avoid spacing variants such as “Bus Terminal” unless required for readability in marketing contexts
- Use consistent casing everywhere

---

## 3.2 Product Tagline Candidates

Potential tagline directions:

- "Operational visibility for Azure messaging"
- "The control plane for Azure Service Bus"
- "Discover, document, and govern your Service Bus estate"
- "Messaging infrastructure intelligence"
- "Where distributed systems become observable"
- "A registry and operations portal for Azure Service Bus"

The implementation phase may refine or replace these.

---

## 3.3 Brand Personality

| Trait | Description |
|---|---|
| Technical | Built for engineers, architects, and operators |
| Reliable | Stable, trustworthy, production-grade |
| Modern | Cloud-native aesthetic and interaction patterns |
| Precise | Dense information presented clearly |
| Efficient | Low-friction UX and workflows |
| Operational | Feels suited for real-world infrastructure management |
| Open | Community-friendly open-source positioning |

Avoid:

- Excessively playful design
- Consumer-social aesthetics
- Overly corporate enterprise templates
- Skeuomorphic visuals
- Excessive gradients/glassmorphism
- Busy dashboards with visual overload

---

# 4. Logo and Identity Direction

## 4.1 Logo Goals

The logo should:

- Be recognizable at small sizes
- Work in monochrome and full color
- Render cleanly in terminal/CLI contexts
- Support favicon/app icon usage
- Reflect messaging, routing, infrastructure, or topology concepts

---

## 4.2 Visual Themes

Potential visual metaphors:

- Transit terminal signage
- Message routing paths
- Switchboards
- Distributed node topology
- Infrastructure maps
- Bus route diagrams
- Signal flow
- Terminal displays
- Hub-and-spoke systems

---

## 4.3 Logo Constraints

The logo must:

- Scale well from 16px to large-format usage
- Support dark and light backgrounds
- Avoid excessive detail
- Avoid trendy AI-generated aesthetics
- Be SVG-first
- Support icon-only and full-wordmark variants

---

## 4.4 Deliverables

The branding implementation phase must produce:

- Full logo
- Compact logo
- SVG assets
- PNG exports
- Favicon set
- Dark/light variants
- Social preview assets
- GitHub repository branding assets

---

# 5. Visual Design Language

## 5.1 Design Style

The BusTerminal UI should adopt:

- Modern infrastructure dashboard aesthetics
- Spacious layouts with strong information hierarchy
- Subtle depth and layering
- Strong typography
- Minimal decorative noise
- Soft rounded corners
- Moderate contrast
- Clear visual grouping
- Data-density aware design

Design inspiration categories:

- Modern cloud platforms
- Observability tooling
- Infrastructure management consoles
- Developer tooling platforms
- Operational dashboards

---

## 5.2 Layout Philosophy

The UI should optimize for:

- Wide desktop operational workflows
- Rapid navigation between entities
- Multi-panel information density
- Progressive disclosure
- Scanability
- Keyboard-driven workflows
- Operational efficiency

Preferred patterns:

- Left navigation rail
- Command/search interfaces
- Detail panels
- Structured data tables
- Expandable entity views
- Drawer-based workflows
- Breadcrumb navigation

Avoid:

- Excessive modal usage
- Deep navigation nesting
- Single-column mobile-first dashboard compromises on desktop
- Overly animated transitions

---

# 6. Color System

## 6.1 Color Philosophy

The palette should:

- Work equally well in dark and light mode
- Prioritize readability and operational clarity
- Support semantic states clearly
- Avoid neon cyberpunk styling
- Support long-duration operational usage

---

## 6.2 Core Palette Direction

### Primary Colors

Suggested direction:

- Deep slate
- Azure blue accents
- Steel gray neutrals
- Muted cyan highlights

Potential anchor tones:

- Deep navy/slate base
- Azure-inspired primary accent
- Cool neutral surfaces
- Muted success/warning/error states

---

## 6.3 Semantic Color Usage

| Semantic Purpose | Requirements |
|---|---|
| Success | Clear but not oversaturated |
| Warning | Distinct and readable in dark mode |
| Error | Accessible contrast ratios |
| Info | Consistent with primary accent |
| Disabled | Reduced emphasis without becoming unreadable |
| Interactive | Obvious hover/focus states |

---

## 6.4 Accessibility Requirements

All colors must:

- Meet WCAG AA minimum contrast standards
- Prefer WCAG AAA where practical
- Preserve semantic meaning in both themes
- Remain distinguishable for color vision deficiencies

---

# 7. Typography System

## 7.1 Typography Goals

Typography should:

- Prioritize readability
- Support dense operational data
- Distinguish metadata from content clearly
- Render cleanly across platforms
- Support code-heavy interfaces

---

## 7.2 Font Recommendations

### Primary UI Font

Recommended categories:

- Inter
- Geist
- IBM Plex Sans
- Similar modern sans-serif system

### Monospace Font

Recommended categories:

- JetBrains Mono
- IBM Plex Mono
- Geist Mono
- Cascadia Mono

Monospace usage:

- Queue names
- Entity identifiers
- Namespace names
- Message metadata
- Correlation IDs
- Connection information
- JSON payloads
- CLI snippets

---

## 7.3 Typography Scale

The design system must define:

- Display
- H1–H6
- Body sizes
- Caption sizes
- Label sizes
- Table typography
- Monospace variants

Typography tokens must be implemented centrally.

---

# 8. Iconography

## 8.1 Icon Requirements

Icons should:

- Be simple and recognizable
- Use consistent stroke widths
- Align with modern infrastructure tooling
- Support small-size readability
- Work in dark/light modes

Preferred icon ecosystem:

- lucide-react

---

## 8.2 Domain-Specific Icons

Custom iconography may be needed for:

- Queues
- Topics
- Subscriptions
- Dead-letter queues
- Message flows
- Topology relationships
- Namespace health
- Relay/routing concepts
- Discovery operations
- Topology mapping

---

# 9. Accessibility Standards

## 9.1 Accessibility Objectives

Accessibility is a foundational requirement.

BusTerminal must target:

- WCAG 2.2 AA compliance minimum
- Keyboard-first operability
- Screen reader compatibility
- Reduced motion support
- Proper semantic structure

---

## 9.2 Required Accessibility Features

### Keyboard Navigation

All interactive components must:

- Be fully keyboard accessible
- Include visible focus states
- Support logical tab ordering
- Avoid keyboard traps

### Screen Readers

Requirements:

- Proper ARIA usage
- Semantic HTML first
- Accessible labels
- Meaningful control descriptions
- Proper heading hierarchy

### Motion

Requirements:

- Respect reduced-motion preferences
- Avoid excessive animation
- Use motion to clarify state only

### Color Accessibility

Requirements:

- Never rely solely on color for meaning
- Include icons/text indicators where needed
- Maintain accessible contrast ratios

---

# 10. Frontend Design System Architecture

## 10.1 Prescriptive Frontend Stack

BusTerminal frontend implementation must use the following baseline stack unless a later architecture decision record explicitly overrides it.

| Area | Standard |
|---|---|
| Framework | Next.js 16.x |
| Router | App Router only |
| Language | TypeScript, strict mode |
| React Model | React Server Components by default |
| Styling | Tailwind CSS v4.x |
| Component Foundation | shadcn/ui |
| Icons | lucide-react |
| Data Tables | TanStack Table |
| Forms | React Hook Form |
| Validation | Zod |
| Charts | Recharts, unless topology/graph requirements need a specialized library |
| Animation | Framer Motion, used sparingly |
| Theme Management | next-themes or equivalent lightweight theme provider |
| Component Documentation | Storybook or equivalent |
| Testing | Playwright for E2E, Vitest/React Testing Library for component tests, axe accessibility checks |

This stack should be treated as the default implementation contract for frontend work.

---

## 10.2 Next.js Implementation Standards

BusTerminal must use the Next.js App Router as the primary application architecture.

Required standards:

- Use the `app/` directory
- Use Server Components by default
- Use Client Components only where interactivity, browser APIs, local state, or event handlers are required
- Keep client boundaries small and explicit
- Prefer route groups for major application areas
- Use nested layouts for durable application chrome
- Use loading and error boundaries for operational UX
- Use metadata APIs for page titles and descriptions
- Prefer server-side data access from backend/API boundaries where appropriate
- Avoid Pages Router patterns

Required app structure direction:

```text
app/
  (marketing)/
  (app)/
    layout.tsx
    dashboard/
    namespaces/
    queues/
    topics/
    discovery/
    settings/
  api/
components/
  ui/
  app-shell/
  data-display/
  forms/
  navigation/
  feedback/
lib/
  design-system/
  utils/
  validation/
  api-client/
hooks/
styles/
```

Implementation guidance:

- Feature routes should compose shared primitives rather than creating one-off UI
- Shared layout components should live outside route folders
- Route-specific components may live near their route when not reusable
- Data-fetching and mutation patterns must be consistent across feature specs

---

## 10.3 shadcn/ui Standards

shadcn/ui must be used as the starting point for BusTerminal's owned component library.

BusTerminal should not treat shadcn/ui as a black-box dependency. Components generated from shadcn/ui become source code owned by the project and must be reviewed, themed, documented, and adapted to BusTerminal standards.

Required standards:

- Use shadcn/ui components as the baseline for primitives
- Keep generated components in `components/ui`
- Modify generated components only through intentional design-system decisions
- Prefer composition over wrapper-heavy abstractions
- Avoid uncontrolled divergence from shadcn/ui conventions
- Document BusTerminal-specific variants and usage expectations
- Use shadcn/ui blocks only after code review and design alignment

Initial shadcn/ui component set should include:

- Button
- Input
- Textarea
- Select
- Checkbox
- Radio Group
- Switch
- Label
- Form
- Dialog
- Sheet
- Drawer, where appropriate
- Dropdown Menu
- Context Menu
- Command
- Tabs
- Card
- Badge
- Alert
- Toast/Sonner
- Tooltip
- Popover
- Separator
- Skeleton
- Table
- Breadcrumb
- Scroll Area
- Resizable Panels

---

## 10.4 Tailwind CSS Standards

Tailwind CSS v4.x must be the styling foundation.

Required standards:

- Use Tailwind utility classes for layout and component styling
- Centralize design tokens through Tailwind/theme configuration
- Avoid arbitrary values unless there is a documented design need
- Avoid page-level bespoke styling that bypasses the design system
- Use semantic token names where possible
- Ensure dark and light mode are first-class
- Use CSS variables for themeable colors and design tokens

Styling rules:

- Prefer reusable component variants over repeated utility clusters
- Use `cn()` or equivalent class merging utility for conditional classes
- Define component variants with class-variance-authority or equivalent where variants are needed
- Do not introduce CSS-in-JS libraries
- Do not introduce a second design system

---

## 10.5 MCP Server Usage for Coding Agents

MCP servers are not part of the BusTerminal application runtime.

They are development-time tools for coding agents and should be referenced as implementation aids, not integrated into the product.

Frontend coding agents should use the following MCP servers when working on UI/frontend tasks:

- Next.js MCP server for framework conventions, routing, rendering, caching, and app architecture guidance
- shadcn/ui MCP server for component installation, component patterns, and registry usage
- Microsoft Learn MCP server for Azure and Microsoft platform guidance
- context7 MCP server for current library documentation and examples

Spec language must avoid saying BusTerminal “integrates with” these MCP servers unless the product itself later exposes MCP functionality.

Preferred language:

- “Coding agents must consult…”
- “Frontend implementation workflows should use…”
- “Agentic development should reference…”

Avoid:

- “BusTerminal integrates with MCP servers”
- “The application depends on MCP servers”
- “MCP servers are runtime dependencies”

---

## 10.6 Design Token System

The implementation must centralize:

- Colors
- Typography
- Spacing
- Radius
- Elevation
- Motion durations
- Border styles
- Layout sizing
- Breakpoints
- Z-index layers
- Focus-ring styles
- Data-visualization colors

Tokens must:

- Support dark/light theming
- Be reusable across components
- Avoid hardcoded values in feature implementations
- Map cleanly to Tailwind and shadcn/ui conventions
- Be documented with examples

---

## 10.7 Additional UI Library Policy

The project should avoid unnecessary UI libraries.

Approved default frontend libraries:

- Next.js
- React
- TypeScript
- Tailwind CSS
- shadcn/ui
- Radix UI as used by shadcn/ui
- lucide-react
- TanStack Table
- React Hook Form
- Zod
- Recharts
- Framer Motion
- next-themes
- class-variance-authority
- clsx
- tailwind-merge

Additional UI libraries require justification when they provide capabilities not covered by the approved stack.

Examples requiring explicit approval:

- Alternative component libraries
- CSS-in-JS frameworks
- Heavy charting suites
- Graph/topology visualization libraries
- Virtualization libraries beyond standard table needs
- Drag-and-drop libraries
- Rich text editors
- Code editor components

# 11. Component Standards

## 11.1 Core Principles

Components must:

- Be composable
- Be reusable
- Avoid feature-specific coupling
- Support accessibility by default
- Prefer server-component compatibility where practical
- Have consistent spacing and interaction behavior

---

## 11.2 Required Primitive Components

Initial component library should include the following BusTerminal-owned primitives and composites.

### Layout

- App shell
- Sidebar
- Top bar
- Page header
- Footer
- Split panels
- Resizable panels
- Page containers
- Section containers
- Entity workspace layout
- Tabs
- Breadcrumbs
- Drawer layout

### Inputs

- Text inputs
- Search inputs
- Command search
- Comboboxes
- Selects
- Multi-selects
- Toggles
- Checkboxes
- Radio groups
- Sliders
- Date/time inputs where required
- JSON editors/viewers

### Data Display

- Tables
- Virtualized tables where needed
- Cards
- Stat panels
- Entity detail panels
- Key-value displays
- Badges
- Status pills
- Code blocks
- JSON viewers
- Timeline views
- Activity feeds
- Relationship summaries
- Topology preview cards

### Feedback

- Toasts
- Alerts
- Empty states
- Loading skeletons
- Error states
- Confirmation dialogs
- Inline validation messages
- Retry affordances

### Navigation

- Command palette
- Search overlays
- Drawer navigation
- Context menus
- Pagination
- Entity breadcrumbs
- Recently viewed entities

### BusTerminal Domain Components

The design system must define domain-specific components for:

- Namespace card
- Queue row/card
- Topic row/card
- Subscription row/card
- Dead-letter status indicator
- Message count indicator
- Health summary indicator
- Discovery job status
- Entity relationship badge
- Environment badge
- Azure resource link
- Metadata key-value panel
- Topology mini-map placeholder

---

## 11.3 Data Table Standards

Because BusTerminal is metadata-heavy, data tables are critical.

Tables must use TanStack Table as the default table engine and BusTerminal-owned presentation components for markup and styling.

Tables must support, where appropriate:

- Type-safe column definitions
- Sorting
- Filtering
- Column visibility
- Sticky headers
- Keyboard navigation
- Row actions
- Bulk actions
- Multi-select operations
- Pagination or virtualization depending on dataset size
- Responsive overflow handling
- Empty, loading, and error states
- Persisted view preferences where useful

Table design rules:

- Avoid raw, unstyled HTML tables for product UI
- Avoid one-off table implementations per feature
- Prefer reusable data-table primitives
- Use monospace typography for entity names, resource IDs, and technical identifiers where helpful
- Keep row density configurable if the implementation effort is reasonable

---

## 11.4 Form Standards

Forms must use React Hook Form and Zod unless a feature spec explicitly justifies another approach.

Required form standards:

- Zod schemas define validation rules
- Form errors must be accessible
- Required fields must be visually and programmatically clear
- Submit states must prevent duplicate actions
- Destructive actions require confirmation
- Long-running operations must provide progress or clear feedback

---

## 11.5 Chart and Visualization Standards

Recharts is the default charting library for standard charts.

Approved chart types:

- Line charts
- Bar charts
- Area charts
- Donut/pie charts only where appropriate
- Small trend indicators
- Operational status summaries

Specialized visualization libraries may be introduced later for topology graphs, dependency maps, or interactive message-flow diagrams, but they require a dedicated decision record.

---

# 12. Layout and Responsive Standards

## 12.1 Primary Target Platforms

Primary target:

- Desktop/laptop operational usage

Secondary target:

- Tablet support

Minimal target:

- Mobile usability for read-only/limited operations

---

## 12.2 Responsive Philosophy

The application should:

- Preserve operational efficiency on wide screens
- Avoid hiding critical information unnecessarily
- Prioritize density without clutter
- Use adaptive layouts intelligently

---

## 12.3 Grid and Spacing

Implementation must define:

- Grid system
- Standard gutters
- Content width rules
- Spacing scale
- Container behavior
- Responsive breakpoints

---

# 13. Theming

## 13.1 Theme Requirements

BusTerminal must support:

- Dark mode
- Light mode
- System preference detection

Dark mode should be considered the primary operational experience.

---

## 13.2 Theme Persistence

Theme preferences should:

- Persist locally
- Respect system defaults initially
- Avoid flash-of-unstyled-theme issues

---

# 14. Interaction Design Standards

## 14.1 UX Principles

Interactions should feel:

- Fast
- Predictable
- Operationally efficient
- Low-friction
- Keyboard-friendly

---

## 14.2 Motion Standards

Motion should:

- Be subtle
- Clarify hierarchy/state
- Avoid theatrical transitions
- Stay performant

Animation durations should generally remain below:

- 250ms for most transitions

---

## 14.3 Loading Experience

Loading UX should:

- Prefer skeletons over spinners
- Preserve layout stability
- Provide optimistic responsiveness where safe

---

# 15. Information Architecture Foundations

## 15.1 Primary Entity Types

The design system must anticipate:

- Service Bus namespaces
- Queues
- Topics
- Subscriptions
- Forwarding relationships
- Message flows
- Consumers/producers
- Discovery jobs
- Environment groupings
- Governance metadata

---

## 15.2 Navigation Concepts

The IA should support:

- Global search
- Hierarchical exploration
- Relationship mapping
- Cross-linking between entities
- Deep linking
- Saved views
- Operational workflows

---

# 16. Documentation and Developer Experience

## 16.1 Design Documentation

The implementation phase must produce:

- Design token documentation
- Component usage documentation
- Accessibility guidance
- Layout standards
- Theme guidance
- Interaction guidelines
- Contribution standards

---

## 16.2 Storybook Requirement

The frontend system should include:

- Storybook or equivalent component documentation system

Story coverage should include:

- States
- Variants
- Accessibility testing
- Dark/light mode
- Responsive behavior

---

# 17. Open Source Branding Considerations

Because BusTerminal is intended as an open-source platform:

The design system should:

- Look professional and community-friendly
- Avoid excessive enterprise vendor branding
- Support GitHub/social visibility
- Support future SaaS evolution without redesign

---

# 18. Non-Goals

This specification does not define:

- Business logic
- Backend APIs
- Authentication flows
- Service Bus discovery implementation
- Data persistence architecture
- Feature-level workflows
- Monetization strategy

---

# 19. Deliverables

Implementation of this spec must produce concrete frontend foundation artifacts, not merely documentation or conceptual guidance.

This spec owns the implementation of the shared UI and frontend foundation for BusTerminal.

Feature-specific pages, workflows, and business functionality belong to later specs.

---

## 19.1 Required Next.js Foundation Artifacts

This spec must produce:

### Application Structure

- Next.js 16.x App Router application structure
- Base `app/` directory organization
- Shared route groups
- Shared layouts
- Root providers
- Theme provider setup
- Error boundaries
- Loading boundaries
- Global metadata configuration

### Shared Layout Infrastructure

Required shared layout artifacts:

- Application shell
- Sidebar navigation
- Header/top navigation
- Footer
- Page container primitives
- Section container primitives
- Responsive content layouts
- Dashboard layout primitives
- Split/resizable panel primitives

### Core Frontend Utilities

Required utility artifacts:

- Shared `cn()` utility
- Shared variant utilities
- Shared formatting utilities
- Shared theme utilities
- Shared accessibility utilities where needed

---

## 19.2 Required Tailwind and Styling Artifacts

This spec must produce:

- Tailwind CSS v4.x setup
- Theme token configuration
- CSS variable strategy
- Global styling foundation
- Typography scale implementation
- Color token implementation
- Spacing token implementation
- Radius/elevation token implementation
- Dark/light theme implementation
- Responsive breakpoint standards

The implementation must avoid ad-hoc styling patterns.

---

## 19.3 Required shadcn/ui Foundation Artifacts

This spec must initialize and establish the BusTerminal-owned component foundation based on shadcn/ui.

Required artifacts:

### shadcn/ui Setup

- shadcn/ui initialization
- Base component generation
- Component theming alignment
- Shared variant standards
- Shared interaction standards

### Required Primitive Components

At minimum, this spec must produce reusable implementations for:

- Button
- Input
- Textarea
- Select
- Checkbox
- Radio Group
- Switch
- Form primitives
- Dialog
- Drawer/Sheet
- Tabs
- Table foundation
- Card
- Badge
- Alert
- Toast/Sonner
- Tooltip
- Popover
- Skeleton
- Breadcrumb
- Scroll Area
- Resizable Panels
- Command palette foundation

These become the baseline primitives for all future frontend feature specs.

---

## 19.4 Required Data and Form Foundations

This spec must produce reusable foundations for:

### Data Tables

- TanStack Table integration
- Shared table primitives
- Shared table toolbar patterns
- Sorting/filtering foundations
- Pagination foundations
- Virtualization support strategy
- Empty/loading/error states

### Forms

- React Hook Form integration
- Zod validation integration
- Shared form field primitives
- Shared validation display patterns
- Shared submission/loading patterns

---

## 19.5 Required Documentation and Storybook Artifacts

This spec must produce:

- Storybook or equivalent setup
- Component usage documentation
- Design token documentation
- Accessibility guidance
- Theming guidance
- Frontend contribution guidance
- Agentic coding implementation guidance

Storybook coverage should include:

- Dark/light modes
- Component states
- Accessibility validation
- Responsive layouts
- Loading/error states

---

## 19.6 Required Testing and Quality Artifacts

This spec must establish:

- Frontend linting rules
- Formatting rules
- Accessibility validation tooling
- Base component test patterns
- Playwright setup foundations
- Component testing foundations

---

## 19.7 Explicit Non-Goals

This spec does NOT implement:

- Service Bus business functionality
- Discovery workflows
- Queue/topic management screens
- Governance workflows
- Environment management workflows
- Production topology visualization
- Feature-specific dashboards
- Backend APIs
- Authentication business logic

Future feature specs must consume the artifacts produced here rather than redefining UI foundations.

---

# 20. Acceptance Criteria

This spec is considered complete when:

- A cohesive visual identity exists
- Dark/light themes are functional
- Core design tokens are implemented
- Tailwind CSS v4.x is configured as the styling foundation
- shadcn/ui primitives are generated, owned, themed, and documented
- The Next.js App Router structure is established
- Server Component and Client Component boundaries are documented
- Shared primitives are available
- Data table primitives are implemented using TanStack Table
- Form primitives are implemented using React Hook Form and Zod
- Accessibility standards are documented and enforced
- Storybook or equivalent is operational
- Frontend standards are documented
- MCP server usage is documented as coding-agent guidance, not runtime application integration
- The UI foundation supports rapid feature implementation
- Agentic coding workflows can consistently generate aligned UI

---

# 21. Future Enhancements

Potential future enhancements:

- CLI theming alignment
- Visualization-specific design language
- Topology graph visualization system
- Branded observability dashboards
- Multi-product branding hierarchy
- Plugin ecosystem UI conventions
- SaaS-specific marketing design system

---

# 22. Implementation Guidance for Agentic Development

When implementing features:

- Reuse primitives before creating new components
- Extend shadcn/ui rather than replacing it
- Prefer composability over abstraction-heavy patterns
- Keep visual language operationally focused
- Maintain consistent spacing and typography
- Avoid feature-specific styling drift
- Validate accessibility continuously
- Keep tokens centralized

Frontend coding agents should:

- Use MCP integrations where applicable
- Follow existing component patterns before introducing new ones
- Favor maintainability over visual novelty
- Optimize for long-term scalability of the design system

---

# End of Spec

