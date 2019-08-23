import Vue, { VNode } from 'vue';
 


import * as tsx from "vue-tsx-support";
import { Component, Prop, Watch } from 'vue-property-decorator';

import "@/assets/less/components/sections.less";

import HeaderLayout from "./HeaderLayout";
import HeroLayout from './HeroLayout';




export interface DefaultLayoutOptions {

}

const color1 = [248, 127, 46];
const color2 = [255, 255, 255];
const diff = [color2[0] - color1[0], color2[1] - color1[1], color2[2] - color1[2]]
const scale = 0.3;



@Component
export default class DefaultLayout extends tsx.Component<DefaultLayoutOptions>{

 

    backgroundColor = "rgb(248, 127, 46)";
    transform = "scale3d(1, 1, 1)";

    onScroll() {
        

        let element = document.scrollingElement as Element;
        let p = element.scrollTop / ((element.scrollHeight - element.clientHeight));
        p = isNaN(p) ? 1 : p;



        this.backgroundColor = `rgb(${color1[0] + diff[0] * p},${color1[1] + diff[1] * p},${color1[2] + diff[2] * p})`;
        this.transform = `scale3d(${1 - scale * p}, ${1 - scale * p}, ${1 - scale * p})`;

        if (element.scrollHeight - element.scrollTop === element.clientHeight) {
            console.log('scrolled');
            document.body.classList.add("scrolled");
        } else {
            document.body.classList.remove("scrolled");
        }


        return p;
    }

    mounted() {
        console.log("mounted defaultlayout");
        document.addEventListener("scroll", (event) => {
            let p = this.onScroll();

        });
    }



    render() {
        return (
            <v-app>
                <HeaderLayout backgroundColor={this.backgroundColor}>
                    <template slot="links">
                        <a href="/" class="link-nav w-nav-link w--current">Info</a>
                        <a href="/blog/" class="link-nav w-nav-link">Blog</a>
                    </template>
                </HeaderLayout>
                <HeroLayout transform={this.transform} backgroundColor={this.backgroundColor} title="DotNET DevOps" subtitle="Deliver software faster" />

                <nuxt />

            </v-app>
        );
    }
}
