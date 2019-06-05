

import * as tsx from "vue-tsx-support";
import DefaultLayout from "@/layout/DefaultLayout";
import FrontPage, { WDynItem } from "@/pages/FrontPage";
import { RouteConfig } from 'vue-router';
//const frontPage = () => import("@/pages/frontPage");


const routes = [
    {
        path: '/',
        redirect: '/home',
        component: DefaultLayout,
        children: [
            {
                path: 'home',
                name: 'home',
                component: FrontPage,
                //props: {
                //    dynItems: [
                //        (<WDynItem title="LetsEncrypt" number="01" />)
                //       // new WDynItem({ props: { title: "LetsEncrypt", number: "01"} })
                //    ]
                //}

            },
            //{
            //    path: "designer",
            //    name: "designer",
            //    component: frontPage
            //},
        ]
    }
] as RouteConfig[];

export default routes;
