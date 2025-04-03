const Hapi = require('@hapi/hapi');
const config = require('./config/config');
const imageRoutes = require('./routes/image');

const init = async () => {
    const server = Hapi.server({
        port: config.port,
        host: 'localhost',
        routes: {
            cors: {
                origin: ['*']
            }
        }
    });

    // Register routes
    server.route(imageRoutes);

    // Start server
    await server.start();
    console.log('Server running on %s', server.info.uri);
};

process.on('unhandledRejection', (err) => {
    console.log(err);
    process.exit(1);
});

init(); 