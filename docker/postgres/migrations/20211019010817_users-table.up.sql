create table users (
    id bigint generated always as identity,
    user_twitch_id character varying(30) not null,
    login_name character varying(30) not null,
    display_name character varying(30) not null,
    balance bigint not null default 0,
    last_updated timestamp without time zone not null,
    primary key(id)
);

ALTER TABLE users OWNER TO drakoapi;
