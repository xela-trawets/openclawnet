namespace ImageGenerator;

/// <summary>
/// All image prompts from docs/design/image-prompts.md with output metadata.
/// </summary>
public static class ImagePrompts
{
    public static readonly string NegativePrompt =
        "no watermarks, no stock photo people, no busy backgrounds, no gradients to dark, " +
        "no neon glow, no cyberpunk, no clipart, no cartoon style, no 3D rendered characters, " +
        "no text unless specified, no dark backgrounds";

    /// <summary>
    /// Path to the .NET brand logo used as img2img reference for branding-heavy prompts.
    /// Resolved relative to the project root at runtime.
    /// </summary>
    public static string DotNetLogoPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "design", "assets", "dotnet-brand", "logo", "dotnet-logo.png"));

    public record ImageSpec(
        string Id,
        string Category,
        string FileName,
        string Prompt,
        int Width,
        int Height,
        string? ReferenceImagePath = null);

    public static readonly ImageSpec[] All =
    [
        // ── 1. Logo / Mascot ──────────────────────────────────────────
        new("1A", "logo", "icon-512.png",
            """
            A minimalist, geometric crab claw icon on a pure white background.
            The claw is stylized — two curved pincers forming an abstract "C" shape,
            rendered in warm coral-orange (#E8590C) with thin circuit-trace lines
            in .NET purple (#512BD4) running along the inner surfaces.
            The claw shape subtly suggests both a bracket symbol { and a crab pincer.
            Clean vector-style rendering with flat color fills and very subtle
            drop shadow for depth. No text. No background pattern.
            Perfectly centered, square composition, ample white space around the icon.
            Style: modern tech logo, Scandinavian design minimalism, geometric precision.
            """, 512, 512, ReferenceImagePath: "dotnet-logo"),

        new("1B", "logo", "lockup-horizontal.png",
            """
            A horizontal logo lockup on a white background. Left side: a minimalist
            geometric crab claw icon in warm coral-orange (#E8590C) with thin
            circuit-trace accents in .NET purple (#512BD4). Right side: the text
            "OpenClawNet" in a clean, modern sans-serif font (similar to Inter or
            Segoe UI) in charcoal (#1E293B), with "Claw" subtly highlighted in
            coral-orange. Below the text, a thin horizontal line in cyan-teal
            (#0891B2). The .NET purple dot on the "i" in "Net" or a small .NET
            diamond logo integrated after the text. Clean, professional, lots of
            white space. No background decoration.
            """, 800, 200, ReferenceImagePath: "dotnet-logo"),

        new("1C", "logo", "favicon-32.png",
            """
            An extremely simplified crab claw icon optimized for tiny display sizes.
            Two bold curved strokes forming an abstract pincer/bracket shape.
            Coral-orange (#E8590C) fill with a single .NET purple (#512BD4) accent
            dot at the joint. No fine details — bold, readable silhouette only.
            Pure white background. Must be recognizable at 16x16 pixels.
            Style: ultra-minimalist icon design, favicon-ready.
            """, 256, 256, ReferenceImagePath: "dotnet-logo"),

        new("1D", "logo", "stacked.png",
            """
            A vertically stacked logo on white background. Top: the geometric crab
            claw icon in coral-orange (#E8590C) with .NET purple (#512BD4) circuit
            accents, sized prominently. Below: "Open" in charcoal, "Claw" in
            coral-orange, "Net" in .NET purple — each word on its own line, clean
            sans-serif font, center-aligned. Subtle cyan-teal (#0891B2) horizontal
            rule between icon and text. Generous white space. No background elements.
            Style: modern brand identity, clean typography.
            """, 400, 504, ReferenceImagePath: "dotnet-logo"),

        // ── 2. Slide Assets ───────────────────────────────────────────
        new("2A", "slides", "bg-title.png",
            """
            A clean, light presentation slide background at 16:9 aspect ratio.
            Predominantly white (#FFFFFF) with a subtle geometric pattern in the
            bottom-right corner: thin intersecting lines and nodes in light
            purple (#EDE9FE) and faint cyan-teal (#0891B2), suggesting a network
            or circuit topology. A bold horizontal stripe of .NET purple (#512BD4)
            runs across the bottom 8% of the image. The top-left area is completely
            clean white space (reserved for title text). A very faint crab-claw
            watermark in light gray (#F1F5F9) sits in the bottom-right corner,
            partially overlapping the geometric pattern. No text. No photos.
            Style: corporate keynote, light theme, minimal, modern.
            """, 1366, 768),

        new("2B", "slides", "bg-section-divider.png",
            """
            A light presentation divider slide background at 16:9. White background
            with a wide vertical band of light purple tint (#EDE9FE) on the left
            third of the image. Inside the purple band, thin geometric circuit-trace
            lines in .NET purple (#512BD4) at 15% opacity. The right two-thirds is
            clean white. A thin horizontal line in cyan-teal (#0891B2) crosses the
            full width at the vertical center. A small coral-orange (#E8590C) crab
            claw icon sits where the teal line meets the edge of the purple band.
            No text. Ample space for section title on the right side.
            Style: clean, structural, light theme presentation.
            """, 1366, 768),

        new("2C", "slides", "bg-demo-transition.png",
            """
            A presentation slide background at 16:9 that signals "live coding ahead."
            Light background transitioning from white on the left to a very subtle
            warm gray (#F8FAFC) on the right. Center of the image: a stylized
            code editor window frame (dark charcoal #0F172A rounded rectangle) shown
            at a slight perspective angle, with blurred colorful code lines inside
            (purple, teal, coral syntax colors). Surrounding the editor window:
            small floating icons in coral-orange — a terminal prompt cursor, a
            crab claw, and a play button triangle. Subtle radiating dotted lines
            from the editor window suggesting energy/activation. Bottom: a
            .NET purple (#512BD4) gradient bar.
            Style: energetic but clean, light theme, developer-focused.
            No text.
            """, 1366, 768),

        new("2D", "slides", "bg-speaker-intro.png",
            """
            A clean, light presentation slide background at 16:9 for speaker
            introductions. White background with two subtle rounded rectangles
            (card shapes) side by side in the center — left card has a very light
            purple (#EDE9FE) fill, right card has a very light teal (#ECFEFF) fill.
            Each card has space for a circular avatar photo at top and text below.
            Between the cards: a thin vertical line in charcoal (#1E293B).
            Bottom-left: small OpenClawNet crab claw icon in coral-orange.
            Bottom-right: Microsoft Reactor logo placeholder area.
            Top: a thin .NET purple (#512BD4) accent line spanning the full width.
            No text, no photos — just the layout frame.
            Style: professional speaker card, light theme, clean.
            """, 1366, 768),

        new("2E", "slides", "bg-closing.png",
            """
            A warm, inviting presentation closing slide background at 16:9.
            White background with a large, semi-transparent crab claw watermark in
            coral-orange (#E8590C) at 8% opacity, centered and filling about 60%
            of the frame. Bottom section: a gradient band from white to light
            purple (#EDE9FE). Top-right corner: subtle geometric nodes and
            connection lines in cyan-teal (#0891B2) at 20% opacity, suggesting
            an active network/community. The overall feel is open and welcoming.
            Large clean area in the center for "Thank You" text and resource links.
            Small .NET purple (#512BD4) accent dots scattered sparingly.
            No text. Style: warm, grateful, professional, light theme.
            """, 1366, 768),

        // ── 3. Blog / Article Headers ─────────────────────────────────
        new("3A", "blog", "series-announcement.png",
            """
            A wide blog header image at 16:9 ratio (1200x675). Light, clean
            composition. Left side: the OpenClawNet crab claw icon in coral-orange
            (#E8590C) at medium size with thin circuit-trace lines extending from
            it across the image to the right. These lines connect to four small
            circular nodes evenly spaced in a horizontal row, each node in a
            different accent color: .NET purple (#512BD4), coral-orange (#E8590C),
            cyan-teal (#0891B2), and a warm gold (#D97706) — representing the four
            sessions. Below the nodes, faint code snippet patterns in light gray.
            White background with a very subtle grid pattern. The overall image reads
            as "a connected journey through four stages."
            No text. Style: modern tech blog, light and airy, minimal.
            """, 1200, 672),

        new("3B", "blog", "session-1-header.png",
            """
            A blog header image at 16:9 (1200x675). Light background. Central
            illustration: a stylized architectural scaffold/framework structure made
            of clean geometric lines in .NET purple (#512BD4) — like a wireframe
            building being assembled. Inside the scaffold: a chat bubble icon in
            coral-orange (#E8590C) and a small server/API icon in cyan-teal
            (#0891B2). Bottom of the scaffold: a database cylinder icon in charcoal.
            Thin dotted connection lines between all elements. Small crab claw icon
            in the bottom-right corner. A "1" badge in a .NET purple circle in the
            top-left area. White background, subtle grid.
            No text. Style: clean tech illustration, light theme, isometric hint.
            """, 1200, 672),

        new("3C", "blog", "session-2-header.png",
            """
            A blog header image at 16:9 (1200x675). Light background. Central
            illustration: a stylized agent brain/hub in .NET purple (#512BD4) at
            center with five tool icons radiating outward in a star pattern —
            a folder icon (file system), a terminal icon (shell), a globe icon
            (web fetch), a clock icon (scheduler), and a wrench icon (general
            tools). Each tool icon in coral-orange (#E8590C) with thin connecting
            lines in cyan-teal (#0891B2) back to the central hub. The connections
            have small directional arrows suggesting data flow. Small crab claw
            icon bottom-right. A "2" badge in a .NET purple circle top-left.
            No text. Style: tool ecosystem diagram, light theme, modern flat design.
            """, 1200, 672),

        new("3D", "blog", "session-3-header.png",
            """
            A blog header image at 16:9 (1200x675). Light background. Central
            illustration: an open book or document icon (representing Markdown
            skills) in coral-orange (#E8590C) with YAML-like metadata lines visible.
            Above it, a brain silhouette outline in .NET purple (#512BD4) with
            small memory nodes (circles) connected by thin lines, suggesting a
            neural network or knowledge graph. To the side: a toggle switch icon
            in cyan-teal (#0891B2) symbolizing "skills on/off." Thin dotted
            lines connect the book to the brain — knowledge feeding into memory.
            Small crab claw icon bottom-right. A "3" badge in a .NET purple circle
            top-left. No text. Style: knowledge/learning visual, light theme, clean.
            """, 1200, 672),

        new("3E", "blog", "session-4-header.png",
            """
            A blog header image at 16:9 (1200x675). Light background. Central
            illustration: a cloud shape outline in .NET purple (#512BD4) with
            the Azure logo inside (simplified). Below the cloud: a small
            on-premises server rack icon in charcoal (#1E293B) connected to the
            cloud by an upward arrow made of dotted cyan-teal (#0891B2) lines.
            Around the cloud: small icons for monitoring (chart), testing
            (checkmark), scheduling (clock), all in coral-orange (#E8590C).
            A rocket or launch icon near the cloud suggesting deployment.
            Small crab claw icon bottom-right. A "4" badge in a .NET purple circle
            top-left. No text. Style: cloud deployment visual, light theme, modern.
            """, 1200, 672),

        new("3F", "blog", "recap-header.png",
            """
            A blog header image at 16:9 (1200x675). Light background. Central
            illustration: four completed milestone markers in a horizontal row,
            connected by a solid line — like a completed roadmap. Each marker is
            a filled circle: first in .NET purple, second in coral-orange, third
            in cyan-teal, fourth in warm gold (#D97706). Above each marker, a
            small representative icon from that session (scaffold, tools, brain,
            cloud). Below the completed line: a subtle checkmark or ribbon
            element in coral-orange (#E8590C) suggesting accomplishment. Small
            crab claw icon bottom-right. White background with a celebratory but
            restrained feel — confetti dots in palette colors at very low opacity.
            No text. Style: achievement/completion, light theme, satisfying.
            """, 1200, 672),

        new("3G", "blog", "session-5-header.png",
            """
            A blog header image at 16:9 (1200x672). Light background. Central
            illustration: three interconnected channel icons — a chat bubble
            (Teams), a browser window (Playwright automation), and a webhook
            lightning bolt — arranged in a triangle formation, all in coral-orange
            (#E8590C). Thin cyan-teal (#0891B2) lines connect the three icons
            bidirectionally with small arrow indicators. Behind them: a faint
            circular "orbit" ring in light purple (#EDE9FE) suggesting an
            event-driven ecosystem. Bottom-left: small crab claw icon. Top-left:
            a "5" badge in a .NET purple (#512BD4) circle. White background, subtle
            dot-grid pattern at 5% opacity. No text.
            Style: event-driven channels visual, light theme, modern flat design.
            """, 1200, 672),

        // ── 4. Social Media Cards ─────────────────────────────────────
        new("4A", "social", "series-card.png",
            """
            Square social media card, clean light design. White background.
            Top: OpenClawNet crab claw icon in coral-orange (#E8590C), centered.
            Middle: four small horizontal pill shapes in palette colors
            (purple #512BD4, coral, teal #0891B2, gold) representing four sessions,
            connected by thin lines. Bottom: wide .NET purple banner bar spanning
            full width for text overlay. Subtle tech icons at low opacity in
            background. No text. Style: modern social card, light theme, bold brand.
            """, 1080, 1080),

        new("4B-1", "social", "session-1-card.png",
            """
            A square social media card (1080x1080), light background. Center:
            a large "1" numeral in .NET purple (#512BD4) with a stylized scaffold
            wireframe structure behind it in thin coral-orange (#E8590C) lines.
            Below the number: a chat bubble icon and a database icon connected
            by a cyan-teal (#0891B2) line. Bottom: a .NET purple bar spanning
            the width. Top-right corner: small crab claw icon in coral-orange.
            Clean, bold, minimal. No text except the large "1".
            Style: session promo card, light theme.
            """, 1080, 1080),

        new("4B-2", "social", "session-2-card.png",
            """
            A square social media card (1080x1080), light background. Center:
            a large "2" numeral in .NET purple (#512BD4) with tool icons
            (folder, terminal, globe, wrench) orbiting around it in coral-orange
            (#E8590C). Thin connecting lines from each tool to the number in
            cyan-teal (#0891B2). Bottom: a .NET purple bar spanning the width.
            Top-right corner: small crab claw icon. Clean, bold, minimal.
            No text except the large "2".
            Style: session promo card, light theme.
            """, 1080, 1080),

        new("4B-3", "social", "session-3-card.png",
            """
            A square social media card (1080x1080), light background. Center:
            a large "3" numeral in .NET purple (#512BD4) with a brain outline
            and open book icon overlapping behind it in coral-orange (#E8590C).
            Small toggle switches and memory nodes in cyan-teal (#0891B2).
            Bottom: a .NET purple bar spanning the width. Top-right corner:
            small crab claw icon. Clean, bold, minimal. No text except
            the large "3".
            Style: session promo card, light theme.
            """, 1080, 1080),

        new("4B-4", "social", "session-4-card.png",
            """
            A square social media card (1080x1080), light background. Center:
            a large "4" numeral in .NET purple (#512BD4) with a cloud shape and
            rocket/launch icon behind it in coral-orange (#E8590C). Azure-style
            cloud outline and monitoring chart elements in cyan-teal (#0891B2).
            Bottom: a .NET purple bar spanning the width. Top-right corner:
            small crab claw icon. Clean, bold, minimal. No text except
            the large "4".
            Style: session promo card, light theme.
            """, 1080, 1080),

        new("4B-5", "social", "session-5-card.png",
            """
            A square social media card (1080x1080), light background. Center:
            a large "5" numeral in .NET purple (#512BD4) with three small channel
            icons orbiting it — a Teams chat bubble, a Playwright browser window,
            and a webhook lightning bolt — all in coral-orange (#E8590C). Thin
            cyan-teal (#0891B2) connecting arcs between the icons and the number,
            suggesting event flow. Bottom: a .NET purple bar spanning the width.
            Top-right corner: small crab claw icon. Clean, bold, minimal.
            No text except the large "5".
            Style: session promo card, light theme.
            """, 1080, 1080),

        new("4C", "social", "join-live-card.png",
            """
            A square social media card (1080x1080), light background with energy.
            White background with subtle radiating dotted circles from the center
            (like a broadcast signal). Center: a bold play-button triangle icon
            inside a rounded rectangle (screen shape) in .NET purple (#512BD4).
            A small "LIVE" dot in coral-orange (#E8590C) pulses in the top-right
            of the screen shape. Below the screen: the crab claw icon and
            connection lines extending left and right. Bottom 25%: a .NET purple
            (#512BD4) solid banner block (text/date will be overlaid). Top area:
            clean white space for title overlay. Small tech sparkle accents in
            cyan-teal. No text. Style: event promotion, energetic but clean,
            light theme.
            """, 1080, 1080),

        new("4D", "social", "recording-card.png",
            """
            A square social media card (1080x1080), light background, calm tone.
            White background. Center: a play-button triangle inside a circle
            (video player icon) in .NET purple (#512BD4). Below it: a horizontal
            progress bar at 100% fill in coral-orange (#E8590C), suggesting a
            completed recording. A checkmark icon in cyan-teal (#0891B2) overlaps
            the top-right of the play button circle. Bottom 20%: a .NET purple
            banner block for text overlay. Small crab claw icon at bottom-right
            of the white area. Very subtle clock icon near the progress bar
            suggesting duration. No text. Style: "watch now" card, satisfying
            completion feel, light theme.
            """, 1080, 1080),

        // ── 5. GitHub Assets ──────────────────────────────────────────
        new("5A", "github", "social-openclawnet.png",
            """
            Wide banner 1280x640 for GitHub social preview. Light background (#F8FAFC).
            Left: OpenClawNet crab claw icon in coral-orange (#E8590C). Center: clean
            white space with "OpenClawNet" in charcoal (#1E293B), "Claw" in coral-orange.
            Right: small tech stack icons (.NET, Blazor, Aspire) in muted purple and teal.
            Thin .NET purple (#512BD4) accent line at bottom. Subtle circuit-trace pattern
            in light gray background. Style: GitHub repo card, professional open-source.
            """, 1280, 640, ReferenceImagePath: "dotnet-logo"),

        new("5B", "github", "social-openclawnet-plan.png",
            """
            A wide banner image at 1280x640 for GitHub social preview. Light
            background (#F8FAFC). Similar to the openclawnet social preview but
            with a "planning/architecture" feel. Left third: crab claw icon in
            coral-orange. Center: clean space for title — "OpenClawNet" with a
            small blueprint/document icon suggesting "plan." Right third: a
            simplified Kanban-style board with four columns (one per session)
            shown as thin rectangles in palette colors (purple, coral, teal, gold).
            Thin dotted connection lines between columns. Bottom: .NET purple
            accent line. The word "OpenClawNet Plan" in charcoal sans-serif,
            "Claw" in coral-orange, "Plan" in cyan-teal.
            Style: project planning, professional.
            """, 1280, 640, ReferenceImagePath: "dotnet-logo"),

        new("5C", "github", "readme-banner.png",
            """
            A wide, slim banner at 1280x320 for a GitHub README hero. White
            background. Center-left: the horizontal OpenClawNet logo lockup — crab
            claw icon in coral-orange (#E8590C) followed by "OpenClawNet" text in
            charcoal with "Claw" in coral-orange. Center-right: the tagline area
            (clean space). A thin cyan-teal (#0891B2) horizontal line underscores
            the full width. Below-right: small muted icons for .NET, Aspire,
            Blazor, and Copilot logos in a horizontal row at reduced opacity.
            Very subtle circuit-trace pattern in the background at 5% opacity.
            No other decoration. Extremely clean and readable at any screen size.
            Style: GitHub README banner, minimal, professional.
            """, 1280, 320, ReferenceImagePath: "dotnet-logo"),

        // ── 6. Web App Assets ─────────────────────────────────────────
        new("6A", "webapp", "app-icon-512.png",
            """
            A minimalist crab claw icon for web application use. Two curved pincer
            strokes in coral-orange (#E8590C) forming an abstract bracket/claw shape.
            A single .NET purple (#512BD4) dot at the joint. Pure white background.
            Designed for extreme clarity at small sizes — no fine details, bold
            silhouette only. Must render crisply at 16x16, 32x32, 180x180, and
            512x512. Style: app icon, ultra-clean, favicon-ready.
            Square.
            """, 512, 512),

        new("6B", "webapp", "loading-spinner.png",
            """
            A simple, centered animation-ready crab claw icon on a white background.
            The claw is in coral-orange (#E8590C), drawn as two bold curved arcs
            forming an open pincer. Around the claw: a thin circular dotted ring in
            .NET purple (#512BD4) suggesting rotation/loading. The ring is broken
            into dashes (like a loading spinner). A small cyan-teal (#0891B2) dot
            sits on the ring as an orbital accent. The image should look like the
            static frame of a loading animation. Clean, centered, generous padding.
            No text. Style: loading indicator, minimal, app-native.
            Square.
            """, 256, 256),

        new("6C", "webapp", "header-logo.png",
            """
            A very small, wide horizontal logo for website navigation. White or
            transparent background. Left: a tiny crab claw icon in coral-orange
            (#E8590C), simplified to two bold strokes. Right: "OpenClawNet" in
            a clean sans-serif font in charcoal (#1E293B), with "Claw" in
            coral-orange. Everything aligned to a single horizontal baseline.
            Must be readable at 200x40 pixels. No decorative elements, no
            tagline, just icon + wordmark. Style: website nav logo, crisp, small.
            """, 256, 128, ReferenceImagePath: "dotnet-logo"),

        // ── 7. Promotion ─────────────────────────────────────────────
        new("7A", "promotion", "promo-1x1.png",
            """
            Square tech event card, clean white background. Center: large geometric
            crab claw icon in coral-orange (#E8590C) with circuit-trace lines in
            purple (#512BD4). Below claw: four small colored dots in a row (purple,
            coral, teal #0891B2, gold #D97706) representing four sessions. Corners:
            faint network nodes in light purple (#EDE9FE) at 10% opacity. Thin
            purple stripe along bottom edge. Minimal, modern, professional, flat
            design, soft shadows. No text. No photos. Square 1:1.
            """, 1080, 1080),

        new("7B", "promotion", "promo-hero.png",
            """
            Wide landscape banner, white background. Left: bold geometric crab claw
            in coral-orange (#E8590C) with purple (#512BD4) circuit traces, facing
            right. Center-right: four hexagonal nodes connected by teal (#0891B2)
            lines with arrows, each with subtle icon (chat, wrench, brain, cloud).
            Hexagons have light purple (#EDE9FE) fills, purple borders. Faint grid
            at 5% opacity. Bottom: light purple gradient band. Clean, modern,
            minimalist, professional. No text. No photos.
            """, 1200, 672),
    ];
}
