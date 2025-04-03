# Build stage
FROM node:18.17.0-alpine AS builder

WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY . .

# Production stage
FROM node:18.17.0-alpine

WORKDIR /app

# Copy package files and install production dependencies
COPY package*.json ./
RUN npm ci --only=production

# Copy built files from builder stage
COPY --from=builder /app/src ./src

# Create uploads directory
RUN mkdir -p uploads

# Set environment variables
ENV NODE_ENV=production

# Expose port
EXPOSE 3000

# Start the application
CMD ["npm", "start"] 