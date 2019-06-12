const webpack = require('webpack');

module.exports = {
    lintOnSave: false,
    chainWebpack: (config) => {
        const svgRule = config.module.rule('svg');

        svgRule.uses.clear();

        svgRule
            .use('vue-svg-loader')
            .loader('vue-svg-loader');
    },
    configureWebpack: {
        // Set up all the aliases we use in our app.
        resolve: {
            alias: {
                'chart.js': 'chart.js/dist/Chart.js'
            }
        },
        plugins: [
            new webpack.optimize.LimitChunkCountPlugin({
                maxChunks: 6
            })
        ]
    },
    pwa: {
        name: 'DotNetDevOps',
        themeColor: '#344675',
        msTileColor: '#344675',
        appleMobileWebAppCapable: 'yes',
        appleMobileWebAppStatusBarStyle: '#344675'
    },
    pluginOptions: {
        i18n: {
            locale: 'en',
            fallbackLocale: 'en',
            localeDir: 'locales',
            enableInSFC: false
        }
    },
    css: {
        // Enable CSS source maps.
        sourceMap: process.env.NODE_ENV !== 'production'
    },
    devServer: {
        https: true,
        host: '0.0.0.0',
        hot: true,
        disableHostCheck: true,
        pfx: "c:/dev/localhost.pfx",
        pfxPassphrase: "123456",
        public: 'http://localhost:8080'
    }
};
