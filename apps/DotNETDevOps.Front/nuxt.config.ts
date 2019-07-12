const VuetifyLoaderPlugin = require('vuetify-loader/lib/plugin')
const nodeExternals = require('webpack-node-externals')

module.exports = {
    /*
    ** Headers of the page
    */
    head: {
        title: 'DotNET DevOps',
        //meta: [
        //    { charset: 'utf-8' },
        //    { name: 'viewport', content: 'width=device-width, initial-scale=1.0, maximum-scale=1.5' },
        //    { hid: 'description', name: 'description', content: 'Deliver software faster with DotNet DevOps' },
        //   // { name: "msapplication-TileColor", content: "#f87f2e" },
        //    { name: "theme-color", content: "#ffffff" }
        //],
        link: [
            { rel: 'icon', type: 'image/x-icon', href: '/favicon.ico' },
            { rel: 'stylesheet', type: 'image/x-icon', href: 'https://fonts.googleapis.com/css?family=Roboto:100,300,400,500,700,900|Material+Icons' },
            { rel: "apple-touch-icon", sizes: "180x180", href: "apple-touch-icon.png" },
            { rel: "icon", type: "image/png", sizes: "32x32", href: "favicon-32x32.png" },
            { rel: "icon", type: "image/png", sizes: "16x16", href: "favicon-16x16.png" },
            { rel: "manifest", href: "site.webmanifest" },
            { rel: "mask-icon", href: "safari-pinned-tab.svg", color: "#f87f2e" }

        ]
    },
    //icon: {
    //    sizes:[16,32]
    //},
    manifest: {
        name: 'DotNet DevOps',
    },
    meta: {
        name: 'DotNet DevOps',
        themeColor: '#344675',
        msTileColor: '#344675',
        appleMobileWebAppCapable: 'yes',
        appleMobileWebAppStatusBarStyle: '#344675',
        workboxPluginMode: 'GenerateSW',
    },
    modules: [
        "@nuxtjs/pwa"
    ],
    workbox: {

    },
    /*
    ** Customize the progress bar color
    */
    loading: { color: '#3B8070' },
    plugins: ['~/plugins/vuetify.ts', '~/plugins/myplugin.ts'],
    /*
    ** Build configuration
    */
    build: {
        parallel: true,
        plugins: [
            new VuetifyLoaderPlugin(),
        ],
        transpile: [/^vuetify/],
        /*
        ** Run ESLint on save
        */
        extend(config, { isDev, isClient }) {
            if (isDev && isClient) {
                config.module.rules.push({
                    enforce: 'pre',
                    test: /\.(js|vue)$/,
                    loader: 'eslint-loader',
                    exclude: /(node_modules)/
                })
            }
            if (process.server) {
                config.externals = [
                    nodeExternals({
                        whitelist: [/^vuetify/]
                    })
                ]
            }

            config.module.rules.filter(r => r.test.toString().includes('svg')).forEach(r => { r.test = /\.(png|jpe?g|gif)$/ })
            config.module.rules.push({
                test: /\.svg$/,
                loader: "vue-svg-loader"
            })
        }
    }
}

