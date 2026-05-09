#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

usage() {
    echo "Usage: $0 \"Post Title\" [YYYY-MM-DD]"
    echo "Example: $0 \"My First Entry\" 2026-05-08"
}

if [[ ${1:-} == "-h" || ${1:-} == "--help" ]]; then
    usage
    exit 0
fi

if [[ $# -lt 1 || $# -gt 2 ]]; then
    usage
    exit 1
fi

TITLE="$1"
DATE_INPUT="${2:-$(date +%F)}"

if ! date -d "$DATE_INPUT" +%F >/dev/null 2>&1; then
    echo "Invalid date: $DATE_INPUT"
    echo "Expected format: YYYY-MM-DD"
    exit 1
fi

NORMALIZED_DATE="$(date -d "$DATE_INPUT" +%F)"
YEAR="$(date -d "$NORMALIZED_DATE" +%Y)"
MONTH="$(date -d "$NORMALIZED_DATE" +%-m)"
DAY="$(date -d "$NORMALIZED_DATE" +%-d)"

SLUG="$(
    echo "$TITLE" |
    tr '[:upper:]' '[:lower:]' |
    sed -E 's/[^a-z0-9]+/-/g; s/^-+//; s/-+$//; s/-+/-/g'
)"

if [[ -z "$SLUG" ]]; then
    echo "Unable to generate slug from title."
    exit 1
fi

CONTENT_RELATIVE_PATH="content/$YEAR/$MONTH/$DAY/$SLUG.html"
CONTENT_ABSOLUTE_PATH="$REPO_ROOT/wwwroot/$CONTENT_RELATIVE_PATH"

if [[ -f "$CONTENT_ABSOLUTE_PATH" ]]; then
    echo "Content file already exists: $CONTENT_ABSOLUTE_PATH"
    exit 1
fi

mkdir -p "$(dirname "$CONTENT_ABSOLUTE_PATH")"

ESCAPED_TITLE="$(printf '%s' "$TITLE" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g')"

cat > "$CONTENT_ABSOLUTE_PATH" <<HTML
<h1>$ESCAPED_TITLE</h1>
<p>Start writing your post content here.</p>
<p>This post was published on $NORMALIZED_DATE.</p>
HTML

echo "Created post: $TITLE"
echo "Slug: $SLUG"
echo "Date: $NORMALIZED_DATE"
echo "Content file: $CONTENT_ABSOLUTE_PATH"
echo "Route: /$YEAR/$MONTH/$DAY/$SLUG.html"
echo ""
echo "No repository update is needed. The app auto-discovers posts from files on build/publish and uses the first h1 as post title."
