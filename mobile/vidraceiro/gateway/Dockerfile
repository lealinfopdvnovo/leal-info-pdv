FROM node:22-slim
ENV NODE_ENV=production GENERATION_ONLY=true HOST=0.0.0.0 PORT=8080
WORKDIR /app
COPY --chown=node:node package.json server.mjs domain.mjs retention.mjs ./
USER node
EXPOSE 8080
CMD ["node", "server.mjs"]
