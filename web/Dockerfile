FROM node:26.12.1-alpine AS build
WORKDIR /src
COPY .npmrc package.json package-lock.json ./
RUN npm ci --registry=https://registry.npmjs.org/
COPY . .
RUN npm run build

FROM nginx:1.29-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html
EXPOSE 80
