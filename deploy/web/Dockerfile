# TimeForYou — web (the exported PWA). Built in CI AFTER `npx expo export -p web`, from the REPO ROOT:
#
#   docker build -f deploy/web/Dockerfile .
#
# The context is the repo root so the Dockerfile can reach both app/dist and deploy/web/Caddyfile;
# Dockerfile.dockerignore (same folder) sends nothing else, in particular not node_modules.
FROM caddy:2-alpine

COPY deploy/web/Caddyfile /etc/caddy/Caddyfile
COPY app/dist /srv

EXPOSE 80
