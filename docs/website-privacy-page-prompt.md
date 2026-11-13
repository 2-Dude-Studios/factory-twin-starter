# Prompt: add the app's privacy policy to 2dudestudios.com

*For a Claude Code session opened in the 2 Dude Studios website project (M29). Copy everything inside the box. The
policy's text is `docs/privacy-policy.md` in this repo; the prompt fetches it from GitHub, so it must be pushed first.*

```text
Add a privacy policy page for our Meta Quest app "Factory VR Safety Training" to this website. Meta's store review
(the Data Use Checkup) reads this page, so it has to be public, plain and stable.

Source text: https://raw.githubusercontent.com/2-Dude-Studios/factory-twin-starter/main/docs/privacy-policy.md
(Markdown). Use it as the page's content, with these changes:
- Drop the italic "Draft revised ..." paragraph under the title and the "(draft for 2 Dude Studios to review and
  host)" part of the title. The page title is "Factory VR Safety Training: Privacy Policy".
- Keep every other section, the table and the wording as they are. Do not add claims about data use, do not soften
  the deletion promise, and do not add "certified" or "compliant" anywhere.
- Fill the placeholders: [company address], [support email], [website], [date of publishing] (use today's date as
  the effective date). Ask me for the address and the support email if they are not already on the site; the support
  email must be a real inbox. Leave "[Age rating to be added once the IARC questionnaire is done.]" in place for now.

First look at how this site is built (framework, routing, layouts, existing legal or policy pages, the footer, the
Manufacturing page) and follow its patterns. Then:
1. Add the page at /privacy/factory-vr-safety-training (or the site's equivalent for nested routes), using the site's
   normal layout, header and footer.
2. It must render as ordinary HTML at that URL over https: no login, no cookie wall or modal covering it, no content
   that only appears after client-side JavaScript runs (pre-render or server-render it), and a 200 status.
3. Page metadata: <title> "Factory VR Safety Training: Privacy Policy | 2 Dude Studios", a one-line meta description,
   a canonical URL, and indexable (no noindex).
4. Link it from the site footer ("Privacy: Factory VR Safety Training", or add it under an existing Privacy/Legal
   group), and from the Manufacturing page wherever the VR training app is mentioned.
5. If the site already has a general privacy policy, link this page from it and leave the general one unchanged.
6. Build the site and run its checks; open the page locally and confirm the table, the deletion section and the
   contact details show correctly on desktop and phone widths.
7. Show me the diff and the final URL. Do not deploy or push until I say so.
```

After it is live, put the URL in the Meta Developer Dashboard under App Submission, Additional Details, Privacy Policy,
then submit the Data Use Checkup (User ID and User Profile, for the global leaderboards).
