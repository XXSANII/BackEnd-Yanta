#!/bin/bash
# อัปโหลดรูปเมนูขึ้น Supabase Storage (bucket: menu-images)
#
# วิธีใช้:
#   export SUPABASE_SERVICE_ROLE_KEY='<service_role key จาก Dashboard > Project Settings > API Keys>'
#   ./scripts/upload-menu-images.sh /Users/sanpon/Documents/Yanta/images
#
# ห้ามใส่ service role key ลงในไฟล์ หรือ commit เข้า git
set -euo pipefail

SUPABASE_URL="https://rwpoygtafyxnunazzisl.supabase.co"
BUCKET="menu-images"
DIR="${1:?ระบุโฟลเดอร์รูป เช่น ./scripts/upload-menu-images.sh /path/to/images}"
: "${SUPABASE_SERVICE_ROLE_KEY:?ต้อง export SUPABASE_SERVICE_ROLE_KEY ก่อน}"

mime_of() {
  case "${1##*.}" in
    jpg|jpeg) echo "image/jpeg" ;;
    png) echo "image/png" ;;
    webp) echo "image/webp" ;;
    svg) echo "image/svg+xml" ;;
    *) echo "" ;;
  esac
}

ok=0
fail=0

for f in "$DIR"/*; do
  [ -f "$f" ] || continue
  name="$(basename "$f")"
  mime="$(mime_of "$name")"

  if [ -z "$mime" ]; then
    echo "SKIP  $name (ชนิดไฟล์ไม่รองรับ)"
    continue
  fi

  # key แบบใหม่ (sb_secret_...) ไม่ใช่ JWT ห้ามส่งใน Authorization
  auth_args=()
  if [[ "$SUPABASE_SERVICE_ROLE_KEY" != sb_* ]]; then
    auth_args=(-H "Authorization: Bearer $SUPABASE_SERVICE_ROLE_KEY")
  fi

  body_file="$(mktemp)"
  code=$(curl -s -o "$body_file" -w '%{http_code}' -X POST \
    "$SUPABASE_URL/storage/v1/object/$BUCKET/$name" \
    ${auth_args[@]+"${auth_args[@]}"} \
    -H "apikey: $SUPABASE_SERVICE_ROLE_KEY" \
    -H "Content-Type: $mime" \
    -H "x-upsert: true" \
    --data-binary "@$f") || code="curl-error"

  if [ "$code" = "200" ]; then
    echo "OK    $SUPABASE_URL/storage/v1/object/public/$BUCKET/$name"
    ok=$((ok + 1))
  else
    echo "FAIL  $name (HTTP $code): $(head -c 300 "$body_file")"
    fail=$((fail + 1))
  fi
  rm -f "$body_file"
done

echo "สำเร็จ $ok ไฟล์ ล้มเหลว $fail ไฟล์"
